$location = Get-Location
$builderPath = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin"
$constructorPath = "C:\Projects\SafeguardDevOpsService\ConstructVersionNumber\bin\Debug\netcoreapp3.1"

& $builderPath\MSBuild.exe $location\ConstructVersionNumber\ConstructVersionNumber.csproj /t:rebuild /verbosity:quiet
& $constructorPath\ConstructVersionNumber.exe $location