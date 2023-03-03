using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPF.PSE.ViewModelLayer;
using System;
using System.Windows.Media;
using System.Collections.Generic;
using System.Collections;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.Data;
using System.Configuration;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using System.Windows.Media.Imaging;
using Common.PSELibrary.CustomObjects;
using Common.PSELibrary.Tool;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace WPF.PSE.Utility.UserControls
{
    //public enum IssuePriority
    //{
    //    [Image(IssuePriorityHelper.CellMergingImagesPath + "Low.png")]
    //    Low,
    //    [Image(IssuePriorityHelper.CellMergingImagesPath + "Medium.png")]
    //    Medium,
    //    [Image(IssuePriorityHelper.CellMergingImagesPath + "High.png")]
    //    High
    //}

    public enum FindExt { SQL, CS, XML }
    public partial class SQLTracking : UserControl, IPSUserControl
    {
        private SQLTrackingViewModel _viewModel = null;
        private UserControl _tabUserControl = null;
        private IDictionary _mCookies;
        private static PowerShell _ps;
        private DataTable ResultData = new DataTable("ResultLog");
        private string _debugingSQL = "";
        private string CurrentKeyWordSearch;
        private string PsErrorMsg;
        private string currentProcess;

        public SQLTracking(IDictionary cookie)
        {
            InitializeComponent();
            // Connect to instance of the view model created by the XAML
            _viewModel = (SQLTrackingViewModel)this.Resources["viewModel"];
            _viewModel.DisplayStatusMessage("...");
            _mCookies = cookie;

            CbxDatabaseName.ItemsSource = _viewModel.LoadDatabaseFromXML();
            CbxDatabaseName.SelectedIndex = 0;

            tableView3.ShownEditor += new EditorEventHandler(tableView3_ShownEditor);
            //tableView3.HiddenEditor += new EditorEventHandler(tableView3_HiddenEditor);
        }
        public double MWidth
        {
            get { return this.Width; }
            set { this.Width = value; }
        }
        public double MHeight
        {
            get { return this.Height; }
            set { this.Height = value + 120; }
        }
        public double TabWidth
        {
            get { return this.Width; }
            set
            {
                //this.Width = value;
            }
        }
        public double TabHeight
        {
            get { return ((TabPageControl)_tabUserControl).Height; }
            set
            {
                this.Height = value + 120;
            }
        }
        public string PSEnvironment { get; set; }
        public string TxtEditor { get; private set; }

        public List<Error> Validated
        {
            get
            {
                return null;
            }
        }

        public string CURRENT_TIMESTAMP { get; private set; }
        private void CreateParameters()
        {
            _ps.AddParameter("defaultDatabaseRootPath", ConfigurationManager.AppSettings["SQLServerPath"]);
            _ps.AddParameter("ServerInstance", Environment.MachineName);
            _ps.AddParameter("DatabaseSource", CbxDatabaseName.Text);
            _ps.AddParameter("DatabaseBackup", CbxDatabaseName.Text + "_Bak");

        }
        private void CreateParametersRestore()
        {
            _ps.AddParameter("defaultDatabaseRootPath", ConfigurationManager.AppSettings["SQLServerPath"]);
            _ps.AddParameter("ServerInstance", Environment.MachineName);
            _ps.AddParameter("DatabaseBackup", CbxDatabaseName.Text);
        }

        private void CreateParameters1(string sql)
        {
            _ps.AddParameter("ServerInstance", Environment.MachineName);
            _ps.AddParameter("sql", sql);
            _ps.AddParameter("DatabaseSource", CbxDatabaseName.Text);
        }
        private void CreateParameters2()
        {
            _ps.AddParameter("ServerInstance", Environment.MachineName);
            _ps.AddParameter("sql", GetQuery1);
            _ps.AddParameter("DatabaseSource", CbxDatabaseName.Text);
        }
        private void CreateParameters3()
        {
            _ps.AddParameter("ServerInstance", Environment.MachineName);
            _ps.AddParameter("sql", GetQueryRestoreTable);
            _ps.AddParameter("DatabaseSource", CbxDatabaseName.Text);
        }
        private void CreateParametersSQLStatment()
        {
            string filter = "";
            string filters = cbxFilterSqltxt.Text.Trim();
            if (filters != "")
            {
                filter = "and sql_statement " + filters;
            }
            string d_sql = $@"SELECT TOP 50 * FROM(SELECT COALESCE(OBJECT_NAME(s2.objectid),'Ad-Hoc') AS Object_Name,
                              execution_count,s2.objectid,
                                (SELECT TOP 1 SUBSTRING(s2.TEXT,statement_start_offset / 2+1 ,
                                  ( (CASE WHEN statement_end_offset = -1
                            THEN (LEN(CONVERT(NVARCHAR(MAX),s2.TEXT)) * 2)
                            ELSE statement_end_offset END)- statement_start_offset) / 2+1)) AS Sql_Statement,
                              SUBSTRING(
                                s3.query_plan,CHARINDEX('<ParameterList>',s3.query_plan),
                                CHARINDEX('</ParameterList>',s3.query_plan) + LEN('</ParameterList>') - CHARINDEX('<ParameterList>',s3.query_plan)
                              ) AS Parameters,
                              last_execution_time
                            FROM sys.dm_exec_query_stats AS s1
                            CROSS APPLY sys.dm_exec_sql_text(sql_handle) AS s2
                            CROSS APPLY sys.dm_exec_text_query_plan(s1.plan_handle, s1.statement_start_offset,
                              s1.statement_end_offset) AS s3
                            ) x
                            WHERE sql_statement NOT like 'SELECT TOP 50 * FROM(SELECT %'
                             and (objectid not like '-%' and objectid is not null)
                             {filter}
                             and last_execution_time >= CONVERT(DATETIME, '{CURRENT_TIMESTAMP}', 102)
                            ORDER BY last_execution_time DESC";
            Clipboard.SetDataObject(d_sql);
            _ps.AddParameter("ServerInstance", Environment.MachineName);
            _ps.AddParameter("sql", d_sql);
            _ps.AddParameter("Command", "short cut");
            _ps.AddParameter("DatabaseSource", CbxDatabaseName.Text);
        }
        private void CreateParameters3(string sql, bool showOutput, PowerShell ps)
        {
            ps.AddParameter("ServerInstance", Environment.MachineName);
            ps.AddParameter("sql", sql);
            if (showOutput)
                ps.AddParameter("Command", "Popup");
            ps.AddParameter("DatabaseSource", CbxDatabaseName.Text);
        }
        private void DataCopyStatusAdded(object sender, DataAddedEventArgs e)
        {
            PsErrorMsg = ((PSDataCollection<VerboseRecord>)sender)[e.Index].ToString();
        }
        /// <summary>
        /// Make a copy of the database
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnMakeCopyDb_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            //Reset timer to the current time
            btnReset_Click(null, null);

            //_viewModel.DisplayPopUpMessage(new MessageView()
            //{
            //    InfoMessage = $"Stop services - Backup {CbxDatabaseName.Text} - Restore to  {CbxDatabaseName.Text}_Bak Please wait.",
            //    InfoMessageTitle = $"Backup {CbxDatabaseName.Text} is in process",
            //    IsInfoMessageVisible = true
            //});

            //prepare databases Copy
            _ps = PowerShell.Create();
            try
            {
                Environment.GetEnvironmentVariable("COMPUTERNAME");

                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"CopyDatabase.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exist.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                _ps.Streams.Error.DataAdded += new EventHandler<DataAddedEventArgs>(Error_DataAdded);
                _ps.AddScript(scriptFile);
                CreateParameters();

                // use asyn to enable status message here
                // PSDataCollection<PSObject> results = new PSDataCollection<PSObject>();
                //results.DataAdded += new EventHandler<DataAddedEventArgs>(Process_GridDataAdded);
                //IAsyncResult rasr = _ps.BeginInvoke<PSObject, PSObject>(null, results, null, AsyncInvoke, null);
                //
                this.Cursor = System.Windows.Input.Cursors.Pen;
                _ps.Invoke();
                //if (PsErrorMsg != "none")
                //    throw new Exception("failed in PS script");
                //_viewModel.DisplayPopUpMessage(new MessageView()
                //{
                //    InfoMessage = $"Completed.",
                //    InfoMessageTitle = "Backup database",
                //    IsInfoMessageVisible = true
                //});
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"Failed. note:{PsErrorMsg}",
                    InfoMessageTitle = "Backup database",
                    IsInfoMessageVisible = true
                });

            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
            }
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void AsyncInvoke(IAsyncResult ar)
        {
            try
            {
                _ps.EndInvoke(ar);

            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"Failed. note:{PsErrorMsg}",
                    InfoMessageTitle = "Backup database",
                    IsInfoMessageVisible = true
                });
            }
            finally
            {
                _ps.Stop();
                _ps?.Dispose();
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"Completed.",
                    InfoMessageTitle = $"{currentProcess} database",
                    IsInfoMessageVisible = true
                });
            }
        }

        private void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            PsErrorMsg = ((PSDataCollection<ErrorRecord>)sender)[e.Index].ToString();
            if (PsErrorMsg.Contains("CursorPosition") || PsErrorMsg.Contains("System.Collections.Hashtable"))
                PsErrorMsg = "none";

        }

        private void UpdateResultGrid(Collection<PSObject> results)
        {
            ResultData.Clear();
            PowerShell _ps2 = PowerShell.Create();
            try
            {
                foreach (PSObject result in results)
                {

                    string time = "";
                    string tablesUpdated = "";
                    if (result.Members["TimeStamp"] != null)
                    {
                        string date = result.Members["TimeStamp"].Value.ToString().Replace(" AM", ":xAM").Replace(" PM", ":xPM");
                        time = date.Replace("x", ((DateTime)(result.Members["TimeStamp"].Value)).Ticks / 100000000000000 + " ");
                    }
                    if (result.Members["TablesUpdated"] != null)
                    {
                        tablesUpdated = string.Concat(CbxDatabaseName.Text, ".dbo.", result.Members["TablesUpdated"].Value?.ToString());
                    }
                    bool testDub = ResultData.Rows.Contains(tablesUpdated);
                    if (!testDub)
                        ResultData.Rows.Add(time,
                                          GetDetailsStatus(result.Members["TablesUpdated"].Value?.ToString(), _ps2),
                                          tablesUpdated,
                                          "Find",
                                          new BitmapImage(new Uri($"pack://application:,,,/WPF.PSE.Common;Component/Images/Search_Black.png", UriKind.Absolute)));

                }
            }
            catch (Exception ex)
            {
                var debugError = ex.Message;
            }

            finally
            {
                _ps2?.Stop();
                _ps2?.Dispose();
            }


            //_ps2.Stop();
            //    _ps2.Dispose();

            ResultGrid.Columns[2].Header = $"Table Mordified ({ResultData.Rows.Count})";
            _viewModel.DisplayStatusMessage($"Process Completed on {this.Name}");
            ResultGrid.ItemsSource = ResultData;
            //ClearPopup();
        }

        private string GetDetailsStatus(string tablesUpdated, PowerShell _ps2)
        {
            if (string.IsNullOrEmpty(tablesUpdated))
                return "";
            string sql = GetSQLLogic(tablesUpdated);
            try
            {
                sql = DetectingChanges(string.Concat(CbxDatabaseName.Text, ".dbo.", tablesUpdated), sql, _ps2, false);
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
            }

            return sql;
        }

        //private ImageSource GetImage(string path, string tableName)
        //{  
        //    return new BitmapImage(new Uri(path, UriKind.Absolute));
        //}

        private string GetQuery1
        {
            get
            {
                return $"Use {CbxDatabaseName.Text}; SELECT distinct sys.tables.name AS [TablesUpdated], st.last_user_update [TimeStamp] " +
                           "FROM sys.tables INNER JOIN " +
                           "sys.dm_db_index_usage_stats AS st ON sys.tables.object_id = st.object_id " +
                           "where last_user_update is not null and " +
                           $"st.last_user_update > CONVERT(DATETIME, '{CURRENT_TIMESTAMP}', 102) order by 1 desc";
            }
        }

        private string GetQueryRestoreTable
        {
            get
            {
                return $@"Use {CbxDatabaseName.Text}; 
declare @startTime DateTime
declare @endTime DateTime
 if exists (select name from sys.tables where name like ('Temp%_backup'))
 Begin
EXEC sp_MSforeachtable @command1 = 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'
EXEC sp_MSforeachtable @command1 = 'ALTER TABLE ? DISABLE TRIGGER ALL'
-- STEP 2: delete
DECLARE @deleteCmd nvarchar(MAX)
SET @deleteCmd = ''
SELECT @deleteCmd = @deleteCmd + CONCAT(' DELETE ', TABLE_CATALOG, '.', TABLE_SCHEMA, '.', TABLE_NAME)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = 'CODIS'
AND CHARINDEX('_Backup', TABLE_NAME) = 0
AND CHARINDEX('_BK', TABLE_NAME) = 0---- other test already backup
AND TABLE_NAME not in ('Country', 'State', 'Job', 'Job_Submit', 'Job_Type')-- TODO: other tables to exclude?
AND TABLE_NAME not in ('ViewTextBackup', 'ViewList', 'Match_Results_Xyz', 'Rank_Results_Xyz', 'Search_Results_Disp_Xyz', 'Rank_Results_Temp_Xyz')-- they are created by other data restore functions
AND TABLE_NAME not in ('NGISS_SearchNum')
AND TABLE_NAME IN(SELECT  distinct sys.tables.name AS TableName FROM sys.tables INNER JOIN sys.dm_db_index_usage_stats AS st ON sys.tables.object_id = st.object_id where last_user_update is not null and st.last_user_update > DATEADD(minute, -{tpToRestore}, GETDATE()))
--AND TABLE_NAME not in ('NGISS_Synchronization')---- exclude NGISS_Synchronization?
--AND TABLE_NAME not like('NGISS_%')
EXECUTE(@deleteCmd)
-- STEP 3: Separate identity insert from non - identity insert
  --set @startTime = GETDATE()
declare @identityTableList table(RowNumber int, IdentityTableFullName varchar(500), IdentityTableName varchar(500), IdentityColumnName varchar(500), IdentityColumnDataType varchar(30))
declare @nonIdentifyTableList table(RowNumber int, TableFullName varchar(500), TableName varchar(500))
insert into @identityTableList(RowNumber, IdentityTableFullName, IdentityTableName, IdentityColumnName, IdentityColumnDataType)
SELECT ROW_NUMBER() OVER(ORDER BY t.TABLE_NAME) as RowNumber, CONCAT(TABLE_CATALOG, '.', TABLE_SCHEMA, '.', TABLE_NAME) , TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS t
where COLUMNPROPERTY(object_id(TABLE_SCHEMA+'.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1
and TABLE_CATALOG = 'CODIS'
AND CHARINDEX('_Backup', t.TABLE_NAME) = 0
AND CHARINDEX('_BK', TABLE_NAME) = 0---- other test already backup
AND TABLE_NAME not in ('Country', 'State')-- TODO: other tables to exclude?
AND TABLE_NAME NOT IN('CODIS_AppConfigSettings') --Handled separately
AND TABLE_NAME not in ('ViewTextBackup', 'ViewList', 'Match_Results_Xyz', 'Rank_Results_Xyz', 'Search_Results_Disp_Xyz', 'Rank_Results_Temp_Xyz')-- they are created by other data restore functions
AND TABLE_NAME not in ('NGISS_SearchNum')
AND TABLE_NAME IN(SELECT  distinct sys.tables.name AS TableName FROM sys.tables INNER JOIN sys.dm_db_index_usage_stats AS st ON sys.tables.object_id = st.object_id where last_user_update is not null and st.last_user_update > DATEADD(minute, -{tpToRestore}, GETDATE()))

insert into @nonIdentifyTableList(RowNumber, TableFullName, TableName)
SELECT ROW_NUMBER() OVER(ORDER BY t.TABLE_NAME) as RowNumber, CONCAT(TABLE_CATALOG, '.', TABLE_SCHEMA, '.', TABLE_NAME), t.TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
AND TABLE_CATALOG = 'CODIS'
AND CHARINDEX('_Backup', t.TABLE_NAME) = 0
AND CHARINDEX('_BK', TABLE_NAME) = 0---- other test already backup
AND TABLE_NAME NOT IN(SELECT IdentityTableName FROM @identityTableList)
AND TABLE_NAME not in ('Country', 'State', 'Job', 'Job_Submit', 'Job_Type')-- TODO: other tables to exclude?
AND TABLE_NAME NOT IN('CODIS_AppConfigSettings') --Handled separately
AND TABLE_NAME not in ('ViewTextBackup', 'ViewList', 'Match_Results_Xyz', 'Rank_Results_Xyz', 'Search_Results_Disp_Xyz', 'Rank_Results_Temp_Xyz')-- they are created by other data restore functions
AND TABLE_NAME not in ('NGISS_SearchNum')
AND TABLE_NAME IN(SELECT  distinct sys.tables.name AS TableName FROM sys.tables INNER JOIN sys.dm_db_index_usage_stats AS st ON sys.tables.object_id = st.object_id where last_user_update is not null and st.last_user_update > DATEADD(minute, -{tpToRestore}, GETDATE()))

DECLARE @insertCmd nvarchar(MAX)

-- STEP 4: Do Non-Identity insert
---- Map column list to each table, to be used to generate the insert command
declare @columnNamesMappedToTableName table(TableName varchar(500), ColumnList varchar(max))

declare @count int
declare @tableName varchar(100)
declare @columnNames nvarchar(max)
DECLARE @rowNumber int
set @rowNumber = 1
select @count = count(*) from @nonIdentifyTableList
while (@rowNumber <= @count)
                    begin
                        SELECT @tableName = TableName
    FROM @nonIdentifyTableList
    where RowNumber = @rowNumber
    set @columnNames = ''
    SELECT @columnNames = @columnNames + CONCAT(QUOTENAME(COLUMN_NAME), ', ')
    FROM INFORMATION_SCHEMA.COLUMNS s join @nonIdentifyTableList t on s.TABLE_NAME = t.TableName
    where t.TableName = @tableName
    -- remove the last char ', '
    set @columnNames = substring(@columnNames, 0, len(@columnNames))
    insert into @columnNamesMappedToTableName(TableName, ColumnList)
    SELECT @tableName, @columnNames
    set @rowNumber = @rowNumber + 1
end
set @insertCmd = ''
SELECT @insertCmd = @insertCmd + CONCAT(' INSERT INTO ', s.TableFullName, '(', t.ColumnList, ') ', ' SELECT ', t.ColumnList, ' FROM ', s.TableFullName, '_Backup ')
FROM @nonIdentifyTableList s join @columnNamesMappedToTableName t on s.TableName = t.TableName

EXECUTE(@insertCmd)
set @insertCmd = ''
--STEP 5: Do identity insert

DECLARE @setIdentityOnCmd nvarchar(MAX)
DECLARE @setIdentityOffCmd nvarchar(MAX)
declare @identityTableFullName nvarchar(500)
declare @identityTableName nvarchar(500)
declare @identityColumnName nvarchar(500)
declare @IdentityColumnDataType nvarchar(30)
declare @ReseedCmd nvarchar(500)
set @rowNumber = 1
select @count = count(*) from @identityTableList
while (@rowNumber <= @count)
                    begin
                        SELECT @identityTableFullName = IdentityTableFullName, @identityTableName = IdentityTableName, @identityColumnName = IdentityColumnName, @IdentityColumnDataType = IdentityColumnDataType

    FROM @identityTableList
    where RowNumber = @rowNumber
    --SET @setIdentityOnCmd = ''
    SELECT @setIdentityOnCmd = CONCAT(' SET IDENTITY_INSERT ', @identityTableFullName, ' ON ')
    set @columnNames = ''

    SELECT @columnNames = @columnNames + CONCAT(QUOTENAME(COLUMN_NAME), ', ')

    FROM INFORMATION_SCHEMA.COLUMNS s join @identityTableList i on s.TABLE_NAME = i.IdentityTableName
    --and s.TABLE_CATALOG = i.TABLE_CATALOG and s.TABLE_SCHEMA = i.TABLE_SCHEMA
    WHERE s.TABLE_NAME = @identityTableName
    -- remove the last char
    set @columnNames = substring(@columnNames, 0, len(@columnNames))
    --SET @insertCmd = ''
    SELECT @insertCmd = CONCAT(' INSERT INTO ', @identityTableFullName, '(', @columnNames, ')', ' SELECT ', @columnNames, ' FROM ', @identityTableFullName, '_Backup ')
    SELECT @setIdentityOffCmd = replace(@setIdentityOnCmd, ' ON ', ' OFF ');

    declare @identityInsertCmd nvarchar(max)
    SELECT @identityInsertCmd = CONCAT(@setIdentityOnCmd, ' ', @insertCmd, ' ', @setIdentityOffCmd)
    EXECUTE(@identityInsertCmd)
    -- Placeholder for tables that don't reseed in the ELSE statement
    IF @identityTableName = 'PLACEHOLDER_VALUE'
        BEGIN
            SELECT  @ReseedCmd = CONCAT('DBCC CHECKIDENT(', @identityTableName, ', RESEED) ')
        END
    ELSE
        BEGIN
            SELECT  @ReseedCmd = CONCAT(' DECLARE @NewSeed ', @IdentityColumnDataType, ' SELECT @NewSeed = ISNULL(MAX(', @identityColumnName, '), 0) FROM ', @identityTableName, ' DBCC CHECKIDENT(', @identityTableName, ', RESEED,  @NewSeed ) ')
        END
    EXECUTE(@ReseedCmd)

    set @rowNumber = @rowNumber + 1
end

-- STEP 6: Restore CODIS_AppConfigSettings excluding dashboard settings
SET @deleteCmd = 'DELETE CODIS_AppConfigSettings'
EXECUTE(@deleteCmd)
SET @deleteCmd = ''

SET @insertCmd = '
INSERT INTO CODIS_AppConfigSettings([ApplicationName],[AttributeName],[AttributeValue],[Description])
                    SELECT[ApplicationName],[AttributeName],[AttributeValue],[Description]
                FROM CODIS_AppConfigSettings_Backup
WHERE[ApplicationName] != ''Dashboard'''
EXECUTE(@insertCmd)
SET @insertCmd = ''

-- STEP 7: enable constraints and triggers
EXEC sp_MSforeachtable @command1 = 'ALTER TABLE ? ENABLE TRIGGER ALL'
EXEC sp_MSforeachtable @command1 = 'ALTER TABLE ? CHECK CONSTRAINT ALL'
END
else
Select Status ='You need to install the backup table first'";

            }
        }

        private void ViewChangesDetails_OnClick(object sender, RoutedEventArgs e)
        {
            string sql = GetSQLLogic(((DevExpress.Xpf.Editors.HyperlinkEdit)(sender)).ActualNavigationUrl.Split('.')[2]);
            try
            {
                _ps = PowerShell.Create();
                DetectingChanges(((DevExpress.Xpf.Editors.HyperlinkEdit)(sender)).ActualNavigationUrl, sql, _ps);
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
            }
        }

        private string DetectingChanges(string table, string sql, PowerShell ps, bool displayOutput = true)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"ExecuteSQL.ps1");
            if (!File.Exists(path))
            {
                throw new Exception($"{path} does not exists.");
            }
            //PS Script file has to be runing no error. No error handle in this code.
            string scriptFile = File.ReadAllText(path);
            ps.Commands.Clear();
            ps.Streams.Verbose.DataAdded += new EventHandler<DataAddedEventArgs>(DataCopyStatusAdded);
            ps.AddScript(scriptFile);
            CreateParameters3(sql, displayOutput, ps);
            if (_ps == ps)
                ProcessUpdatedResult(_ps.Invoke(), table);
            else
                sql = ProcessUpdateAlldResult(ps.Invoke(), table);
            return sql;
        }

        private void ProcessUpdatedResult(Collection<PSObject> collection, string tablesUpdated)
        {
            string filter = "";
            if (collection.Count == 0)
            {
                GetFilterResultWithDataUpdate(ResultData, tablesUpdated, "Undefined. (No Changes/XML Column)", null);
                Clipboard.SetDataObject(_debugingSQL.Replace("SQLFilter", filter).Replace("Union", ""));
                return;
            }

            filter = GetFilterResultWithDataUpdate(ResultData, tablesUpdated, GetUpdateStatus(collection), collection);

            Clipboard.SetDataObject(_debugingSQL.Replace("SQLFilter", filter));
        }
        private string ProcessUpdateAlldResult(Collection<PSObject> collection, string tablesUpdated)
        {
            if (collection.Count == 0)
            {
                return "Undefined. (No Changes/XML Column)";
            }

            return GetUpdateStatus(collection);
        }

        private string GetUpdateStatus(Collection<PSObject> collection)
        {
            int backupCount = 0;
            int original = 0;
            foreach (PSObject result in collection)
            {
                if (result.Members["sourceData"].Value.ToString() == "Baseline")
                    backupCount++;
            }
            original = collection.Count - backupCount;
            if (original > backupCount)
                return "Inserted";
            else if (original == backupCount)
                return "Modified (include Inserted/Deleted)";
            else
                return "Deleted";

        }
        private string GetFilterResultWithDataUpdate(DataTable resultData, string tablesUpdated, string resultText, Collection<PSObject> collection)
        {
            string filter = "WHERE ";
            Hashtable columnValues = new Hashtable();

            foreach (DataRow row in resultData.Rows)
            {
                if (row["TablesUpdated"].ToString() != tablesUpdated)
                    continue;
                else
                    row["Result"] = resultText;

                if (collection == null)
                    continue;

                foreach (PSObject result in collection)
                {
                    int skip = 0;
                    try
                    {
                        foreach (var m in result.Members)
                        {
                            if (skip++ == 0)
                                continue;
                            if (columnValues[m.Name] == null)
                                columnValues.Add(m.Name, result.Members[m.Name].Value?.ToString());
                            else
                            {
                                if (!columnValues[m.Name].ToString().Contains(result.Members[m.Name].Value?.ToString()))
                                    columnValues[m.Name] = columnValues[m.Name] + "," + result.Members[m.Name].Value?.ToString();
                            }
                            break;
                        }
                    }
                    catch //(Exception ex)
                    {
                        continue;
                    }

                }
            }
            _viewModel.DisplayStatusMessage($"Completed Analyze {tablesUpdated}");
            ResultGrid.ItemsSource = ResultData;
            return CleanUpFilter(filter, columnValues);
        }

        private string CleanUpFilter(string filter, Hashtable columnValues)
        {
            IEnumerator enumerator = columnValues.Keys.GetEnumerator();
            if (enumerator.MoveNext())
            {
                object first = enumerator.Current;
                return filter + first + " in(" + columnValues[first] + ")";
            }
            return "";
        }

        private string GetSQLLogic(string tableSource, bool changeInnerJoin = false, string columnSelect = "")
        {
            string tableWOSchema = tableSource.Split('.')[tableSource.Split('.').Length - 1];
            columnSelect = "'Changed' as sourceData, *"; // GetTheTableKeyValue(tableWOSchema);
            string columnSelect2 = "'Baseline' as sourceData, *";

            _debugingSQL = $"Use {CbxDatabaseName.Text};\r\n Select {columnSelect} from {tableSource} SQLFilter\r\n Union \r\nSelect {columnSelect2} from {CbxDatabaseName.Text}_bak.dbo.{tableSource} SQLFilter";

            return $"Use {CbxDatabaseName.Text};\r\n (Select {columnSelect2} from {CbxDatabaseName.Text}_bak.dbo.{tableWOSchema}\r\n EXCEPT \r\nSelect {columnSelect2} from {tableSource}) " +
            $" \r\nUnion \r\n (Select {columnSelect} from {tableSource}\r\n EXCEPT \r\nSelect {columnSelect} from {CbxDatabaseName.Text}_bak.dbo.{tableSource})";
        }

        private string GetTheTableKeyValue(string tableWOSchema)
        {
            string sql = $"Use {CbxDatabaseName.Text}; SELECT Col.Column_Name from " +
                "INFORMATION_SCHEMA.TABLE_CONSTRAINTS Tab, " +
                "INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE Col " +
            "WHERE " +
                "Col.Constraint_Name = Tab.Constraint_Name " +
                "AND Col.Table_Name = Tab.Table_Name " +
                "AND Constraint_Type = 'PRIMARY KEY' " +
                "AND Col.Table_Name = '" + tableWOSchema + "'";
            _ps = PowerShell.Create();
            try
            {
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"ExecuteSQL.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                _ps.AddScript(scriptFile);
                CreateParameters1(sql);
                sql = GetExeScallar(_ps.Invoke());
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);

            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
            }
            return sql;
        }

        private string GetExeScallar(Collection<PSObject> collection)
        {
            string reString = "*";
            if (collection == null)
                return reString;
            foreach (PSObject result in collection)
            {
                try
                {
                    if (reString == "*")
                        reString = result.Members["Column_Name"].Value?.ToString();
                    else
                        reString += "," + result.Members["Column_Name"].Value?.ToString();
                }
                catch //(Exception ex)
                {
                    continue;
                }
            }
            return reString;
        }

        private void SetGridInitialLayout(object sender, RoutedEventArgs e)
        {
            if (ResultData.Columns.Contains("TimeStamp"))
                return;
            var keys = new DataColumn[1];
            ResultData.Columns.Add("TimeStamp", typeof(string));// typeof(DateTime));            
            ResultData.Columns.Add("Result", typeof(string));
            ResultData.Columns.Add("TablesUpdated", typeof(string));
            ResultData.Columns.Add("CodeFiles", typeof(string));
            ResultData.Columns.Add("SearchText", typeof(ImageSource));
            keys[0] = ResultData.Columns[2];
            ResultData.PrimaryKey = keys;
            //ResultData.Columns.Add("UpdateStatus", typeof(ImageSource));
            CURRENT_TIMESTAMP = DateTime.Now.AddDays(-4).ToString();
        }
        /// <summary>
        /// Generate result sqlTracking
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewChanges_OnClick(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            ResultData.Rows.Clear();
            int minute = 0;
            if (((Button)sender).Name == "startTimerCmd")
            {
                if (StartNowTimer == DateTime.MinValue)
                {
                    return;
                }
                var stopTime = DateTime.Now.Subtract(StartNowTimer);
                CURRENT_TIMESTAMP = DateTime.Now.Subtract(stopTime).ToString();
                
            }
            else { 
                minute = Utils.RemoveChars(((Button)sender).Content.ToString());
                if (minute == 0)
                    minute = 10000;
                CURRENT_TIMESTAMP = DateTime.Now.Subtract(new TimeSpan(0, minute, 0)).ToString();
                StartNowTimer = DateTime.Now.Subtract(new TimeSpan(0, minute, 0));
            }
            //_viewModel.DisplayPopUpMessage(new MessageView()
            //{
            //    InfoMessage = $"Find updated to {CbxDatabaseName.Text}. Please wait.",
            //    InfoMessageTitle = $"Search {CbxDatabaseName.Text} is in process",
            //    IsInfoMessageVisible = true
            //});
            _ps = PowerShell.Create();
            try
            {
                //if (runspace != null)
                //    _ps.Runspace = runspace;
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"ExecuteSQL.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                //_ps.Streams.Verbose.DataAdded += new EventHandler<DataAddedEventArgs>(DataCopyStatusAdded);
                _ps.AddScript(scriptFile);
                CreateParameters2();
                //_ps.Streams.Error.DataAdded += new EventHandler<DataAddedEventArgs>(Error_DataAdded);
                UpdateResultGrid(_ps.Invoke());
                //if (PsErrorMsg != "none")
                //    throw new Exception("failed in PS script");
                //currentProcess = " Search ";
                //_viewModel.DisplayPopUpMessage(new MessageView()
                //{
                //    InfoMessage = $"Completed.",
                //    InfoMessageTitle = "Search database",
                //    IsInfoMessageVisible = false
                //});
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"Failed.",
                    InfoMessageTitle = "Search changes to the database",
                    IsInfoMessageVisible = true
                });
            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            return;
        }
        /// <summary>
        /// Get the primarykey
        /// </summary>

        //        SELECT Col.Column_Name from
        //    INFORMATION_SCHEMA.TABLE_CONSTRAINTS Tab,
        //    INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE Col

        //WHERE
        //    Col.Constraint_Name = Tab.Constraint_Name

        //    AND Col.Table_Name = Tab.Table_Name

        //    AND Constraint_Type = 'PRIMARY KEY'

        //    AND Col.Table_Name = 'Specimen'

        /// <summary>
        ///  Get table Created
        /// </summary>
        //        SELECT 'CODIS' AS dbname, t1.table_name
        //        FROM CODIS.[INFORMATION_SCHEMA].[tables]
        //        t1
        //        WHERE table_name NOT IN
        //             (SELECT t2.table_name
        //               FROM CODIS_Bak.[INFORMATION_SCHEMA].[tables] t2

        //             )
        //UNION
        //SELECT 'CODIS_Bak' AS dbname, t1.table_name
        //FROM CODIS_Bak.[INFORMATION_SCHEMA].[tables]
        //        t1
        //WHERE table_name NOT IN
        //     (SELECT t2.table_name
        //       FROM CODIS.[INFORMATION_SCHEMA].[tables] t2
        //     )

        private void btnSet_Click(object sender, RoutedEventArgs e)
        {
            CURRENT_TIMESTAMP = DateTime.Now.ToString();
            ResultData.Rows.Clear();
        }
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            CURRENT_TIMESTAMP = DateTime.MinValue.ToString();
            ResultData.Rows.Clear();
        }
        private void BtnRestoreDB_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            _ps = PowerShell.Create();
            bool process1_StopService = true;
            ToggleServiceForDBRestore(_ps, true, ref process1_StopService);
            try
            {
                //_viewModel.DisplayPopUpMessage(new MessageView()
                //{
                //    InfoMessage = $"Stop services - Restore {CbxDatabaseName.Text}_Bak - to  {CbxDatabaseName.Text} Please wait.",
                //    InfoMessageTitle = $"Restore {CbxDatabaseName.Text} is in process",
                //    IsInfoMessageVisible = true
                //});
                while (process1_StopService) ;
                this.Cursor = System.Windows.Input.Cursors.Pen;
                _ps = PowerShell.Create();
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"RestoreDatabase.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                //_ps.Streams.Error.DataAdded += new EventHandler<DataAddedEventArgs>(Error_DataAdded);
                _ps.AddScript(scriptFile);
                CreateParametersRestore();
                currentProcess = " Restore ";
                //PSDataCollection<PSObject> results = new PSDataCollection<PSObject>();     

                // IAsyncResult rasr = _ps.BeginInvoke<PSObject, PSObject>(null, results, null, AsyncInvoke, null);
                _ps.Invoke();

                ToggleServiceForDBRestore(_ps, false, ref process1_StopService);

                //if (PsErrorMsg != "none")
                //    throw new Exception("failed in PS script");
                //_viewModel.DisplayPopUpMessage(new MessageView()
                //{
                //    InfoMessage = $"Completed.",
                //    InfoMessageTitle = "Restore database",
                //    IsInfoMessageVisible = true
                //});

            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"{PsErrorMsg}",
                    InfoMessageTitle = "Restore database",
                    IsInfoMessageVisible = true
                });

            }
            finally
            {
                _ps?.Stop();
                _ps?.Dispose();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void ToggleServiceForDBRestore(PowerShell ps, bool isStop, ref bool process1_StopService)
        {
            this.Cursor = System.Windows.Input.Cursors.Pen;
            try
            {
                //if (runspace != null)
                //    _ps.Runspace = runspace;
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", isStop ? @"StopServices.PS1" : "StartServices.PS1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                ps.Commands.Clear();
                ps.AddScript(scriptFile);
                currentProcess = " Stop service ";
                //PSDataCollection<PSObject> results = new PSDataCollection<PSObject>();
                // IAsyncResult rasr = _ps.BeginInvoke<PSObject, PSObject>(null, results, null, AsyncInvoke, null);
                var test = ps.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
            }
            finally
            {
                process1_StopService = false;
                if (!isStop)
                {
                    ps?.Stop();
                    ps?.Dispose();
                }
            }
        }


        private string CreateParametersTemp(string script, string filter = @"SetKit.* kitName", string ext = @"\\*.sql")
        {
            script = script.Replace("***", ext);
            script = script.Replace("###", filter);
            return script;
        }

        //   [Command]
        public void OnSelectedGeoDocument(HyperlinkEditRequestNavigationEventArgs args)
        {
            var test = args.Source;

            // use args.Source to get access to the HyperlinkEdit object
        }

        //...
        void tableView3_ShownEditor(object sender, EditorEventArgs e)
        {
            if (e.Column.FieldName == "CodeFiles")
            {
                ((ComboBoxEdit)tableView3.ActiveEditor).SelectedIndexChanged -= ComboBoxEdit_SelectedIndexChanged;
                ((ComboBoxEdit)tableView3.ActiveEditor).SelectedIndexChanged += ComboBoxEdit_SelectedIndexChanged;
            }
            CurrentKeyWordSearch = ((DataRowView)(e.Row)).Row["TablesUpdated"].ToString().Split('.')[2];
        }
        void ComboBoxEdit_SelectedIndexChanged(object sender, RoutedEventArgs e)
        {
            ComboBoxEdit cbx = e.OriginalSource as ComboBoxEdit;
            var fileType = @"\\*." + cbx.SelectedItem.ToString();
            try
            {
                _ps = PowerShell.Create();
                _ps.Commands.Clear();
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"ExecuteTextSearch.PS1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);

                _ps.AddScript(CreateParametersTemp(scriptFile, CurrentKeyWordSearch, fileType));// @"SetKit.* kitName"


                var test = _ps.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);

            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
            }
        }

        private void BtnGetLastQuery_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            _ps = PowerShell.Create();
            try
            {
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"ExecuteSQL.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                _ps.AddScript(scriptFile);
                CreateParametersSQLStatment();
                this.Cursor = System.Windows.Input.Cursors.Pen;
                _ps.Invoke();

            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"Failed.",
                    InfoMessageTitle = "Search changes to the database",
                    IsInfoMessageVisible = true
                });
            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            return;
        }
        private string tpToRestore = "600";
        private DateTime _startNowTimer;
        private DateTime StartNowTimer
        {
            get { return _startNowTimer; }
            set
            {
                _startNowTimer = value;
                btTimer.Content = "Start Timmer " + value.Hour + ":" + +value.Minute + ":" + value.Second;
            }
        }

        private void BtnRestoreTables_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Pen;

            _ps = PowerShell.Create();
            try
            {
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"ExecuteSQL.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{path} does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                _ps.AddScript(scriptFile);

                CreateParameters3();
                GetExeScallar(_ps.Invoke());
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);

            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
            }
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        private void BtnLatestDb_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            _ps = PowerShell.Create();
            try
            {
                Environment.GetEnvironmentVariable("COMPUTERNAME");
                string pathAndFileName = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"restoreCODISDatabase.PS1");
                if (!File.Exists(pathAndFileName))
                {
                    throw new Exception($"{pathAndFileName} does not exist.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(pathAndFileName);
                _ps.Commands.Clear();
                _ps.Streams.Error.DataAdded += new EventHandler<DataAddedEventArgs>(Error_DataAdded);
                _ps.AddScript(scriptFile);
                CreateParameters();
                this.Cursor = System.Windows.Input.Cursors.Pen;
                _ps.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                _viewModel.DisplayPopUpMessage(new MessageView()
                {
                    InfoMessage = $"Failed. note:{PsErrorMsg}",
                    InfoMessageTitle = "Backup database",
                    IsInfoMessageVisible = true
                });

            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
            }
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void SetMouseWait(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Pen;
        }

        private void StartNow_OnClick(object sender, RoutedEventArgs e)
        {
            this.StartNowTimer = DateTime.Now;
        }
    }
}
