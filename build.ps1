cd $PSScriptRoot
$artifacts = 'artifacts'
if($artifacts | Test-Path){
    Remove-Item $artifacts -Recurse -Force
}
else {
    mkdir $artifacts
}
dotnet clean
dotnet restore
dotnet build
pause