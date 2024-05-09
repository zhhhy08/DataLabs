$currentHooksPath = git config --get core.hooksPath
$hooksPath = '.githooks'
if ($currentHooksPath -ne $hookspath) {
  git config core.hooksPath 
  New-Item -ItemType Directory $hookspath -ErrorAction SilentlyContinue -Force
  Copy-Item .\.config\pre-commit -Destination $hookspath -Force

  Get-ChildItem $hookspath -Recurse

}
else {
  git config core.hooksPath .\githooks
  New-Item -ItemType Directory $hookspath -ErrorAction SilentlyContinue -Force
  Copy-Item .\.config\pre-commit -Destination $hookspath -Force

  Get-ChildItem $hookspath -Recurse

}

