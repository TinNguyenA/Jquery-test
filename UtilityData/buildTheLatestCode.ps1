
 Set-Alias buildCode "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
 Set-Alias tf "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\tf.exe"
 
 #//get the latest  
  tf get C:\Proj\R\CODIS\Trunk\Source\Product /recursive

 #// Build the latest
 buildCode C:\Proj\R\CODIS/Trunk/Source/Product/CODIS.sln /t:Build /p:Configuration=Debug /p:Platform=x64
  
