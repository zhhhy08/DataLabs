function Format-Json {
  Param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
    [string]$Json,

    [ValidateRange(1, 4)]
    [int]$Indentation = 4

  )

  if ($PSCmdlet.ParameterSetName -eq 'Minify') {
    return ($Json | ConvertFrom-Json) | ConvertTo-Json -Depth 100 -Compress
  }

  # If the input JSON text has been created with ConvertTo-Json -Compress
  # then we first need to reconvert it without compression
  if ($Json -notmatch '\r?\n') {
    $Json = ($Json | ConvertFrom-Json) | ConvertTo-Json -Depth 100
  }

  $indent = 0
  $regexUnlessQuoted = '(?=([^"]*"[^"]*")*[^"]*$)'

  $result = $Json -split '\r?\n' |
  ForEach-Object {
    # If the line contains a ] or } character, 
    # we need to decrement the indentation level unless it is inside quotes.
    if ($_ -match "[}\]]$regexUnlessQuoted") {
      $indent = [Math]::Max($indent - $Indentation, 0)
    }

    # Replace all colon-space combinations by ": " unless it is inside quotes.
    $line = (' ' * $indent) + ($_.TrimStart() -replace ":\s+$regexUnlessQuoted", ': ')

    # If the line contains a [ or { character, 
    # we need to increment the indentation level unless it is inside quotes.
    if ($_ -match "[\{\[]$regexUnlessQuoted") {
      $indent += $Indentation
    }

    $line | Where-Object { [system.string]::IsNullOrWhiteSpace($_) -ne $true }
  }

  return $result -Join [Environment]::NewLine
}
$utf8 = New-Object System.Text.UTF8Encoding $false
$fx1 = Get-ChildItem *.json -Recurse -Path .\src\Ev2Deployment
foreach ($fx in $fx1) {
  $c = (Get-Content $fx.fullname -Raw -Encoding utf8 | Format-Json) 
  If ($c.length -ge 1) {
    [Io.File]::WriteAllText($fx.fullname, ($c), $utf8)
  }
  Else {
    throw "Content Length is 0 for $($fx.fullname)"
  }
}
