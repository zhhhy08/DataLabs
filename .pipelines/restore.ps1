# Downloading Crane here so that the EV2 shell has what it needs to push to the ACR, putting it in with script
Invoke-WebRequest -Uri https://github.com/google/go-containerregistry/releases/download/v0.4.0/go-containerregistry_Linux_x86_64.tar.gz -OutFile src/Ev2Deployment/Scripts/Shell/crane.tar.gz
pushd src/Ev2Deployment/Scripts/Shell
dir 
tar xzvf crane.tar.gz
popd

# packaging everything together: script and crane
7z a -ttar out/Ev2Deployment/ServiceGroupRoot/Run.tar ./src/Ev2Deployment/Scripts/Shell/*

# EV2 doesn't handle non utf8 characters well and the encoding powershell uses
# varies a lot between version to version.  Using this dot net method should produce a utf8 w/o BOM encoding.
$versionContent = $env:BUILD_BUILDNUMBER
$versionFileName = ".\src\Ev2Deployment\ServiceGroupRoot\BuildVer.txt"

# If the file already exists, delete it so you can recreate it and repopulate with the build number
if (Test-Path -Path $versionFileName -PathType Leaf) {
    Remove-Item -Path $versionFileName
}

# Create the empty file so we can resolve the path
New-Item -Name $versionFileName -ItemType File

# WriteAllLines requires an absolute path
$versionFile = Convert-Path $versionFileName
[IO.File]::WriteAllText($versionFile, $versionContent)

# Replace build version [[<BUILD_VERSION>]] with $env:BUILD_BUILDNUMBER
$files = Get-ChildItem *.yaml -Recurse -Path ./src/AKSDeployment/Charts
foreach ($file in $files) {
    $c = (Get-Content $file.fullname -Raw) 
    If ($c.length -ge 1) {
        $content = $c.Replace("[[<BUILD_VERSION>]]",$versionContent)
        [IO.File]::WriteAllText($file.fullname, $content)
        "$file.fullName has [[<BUILD_VERSION>]] replaced with $versionContent"
    }
    Else {
      throw "Content Length is 0 for $($fx.fullname)"
    }
}

#packaging charts to a tar file for application deployment
7z a -ttar out/Ev2Deployment/ServiceGroupRoot/charts.tar ./src/AKSDeployment/Charts/* 