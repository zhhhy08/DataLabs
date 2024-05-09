cls
function get-delete($method){
    $fs = (Get-ChildItem -Path .\out -Directory -Recurse -Depth 1).fullname
    write-host "$method has below files"
    $fs 
    $fs|remove-item -recurse -force -ErrorAction SilentlyContinue|out-null
}
$truelist = @('"dirs.proj"','/p:platform="x64"','/p:configuration="release"','/v:m','/p:IsOfficialBuild=true')
$falselist = @('dirs.proj','/p:platform="x64"','/p:configuration="release"','/v:m','/p:IsOfficialBuild=false')
$nullList = @('dirs.proj','/p:platform="x64"','/p:configuration="release"','/v:m')
$msbuild = '"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\msbuild.exe"'
invoke-expression "& $msbuild $($truelist -join ' ')" | out-null

get-delete -method "true"

invoke-expression "& $msbuild $($falselist -join ' ')" | out-null
get-delete -method "false"

invoke-expression "& $msbuild $($nullList -join ' ')" | out-null
get-delete -method "null"