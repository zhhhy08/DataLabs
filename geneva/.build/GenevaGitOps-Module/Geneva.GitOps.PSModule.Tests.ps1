# unit tests for Geneva.GitOps.PSModule
# use `InModuleScope "Geneva.GitOps.PSModule" { ... }` to test private module functions. 
# You will need to declare any global variables needed inside the InModuleScope block.

BeforeAll {
    Import-Module "./Geneva.GitOps.PSModule.psm1" -Force
    $testDataPath = "$PSScriptRoot/testdata"

    # copy of private method from Geneva.GitOps.PSModule.psm1
    # it's useful for other tests, but we don't want to work within module scope for all of the tests
    # just the tests that touch the internal functions
    # NOTE: IF YOU UPDATE, update the GetFileAsJson method in the psm1 file
    Function GetAsJson ($path) {
        $content = Get-Content -Raw -LiteralPath $path -ErrorAction Stop

        # versions 6 and above support comments in json files
        if (6 -ile $PSVersionTable.PSVersion.Major) {
            return $content | ConvertFrom-Json
        }

        # remove the comments from the json file
        return $content -replace '(?m)(?<=^([^"]|"[^"]*")*)//.*' -replace '(?ms)/\*.*?\*/' | ConvertFrom-Json
    }

    # Mock the Write-HostWithInfo function to do nothing much, but still throw if needed
    Mock -Module "Geneva.GitOps.PSModule" Write-HostWithInfo { param(
            [string] $section,
            [string] $message,
            [bool] $Throw = $false
        ) 
        if ($Throw -eq $true) { throw "Test-HostWithInfo" }
    }
}

Describe "Test-OrCreatePath" -Tag "Internal" {
    It "Should not throw an error when PackagesRoot exists" {
        InModuleScope "Geneva.GitOps.PSModule" {
            # InModuleScope, re-define $testDataPath
            $testDataPath = "$PSScriptRoot/testdata"

            { Test-OrCreatePath -Path $testDataPath } | Should -Not -Throw
        }
    }

    It "Should create the directory and not throw an error when PackagesRoot does not exist" {
        InModuleScope "Geneva.GitOps.PSModule" {
            # InModuleScope, re-define $testDataPath
            $testDataPath = "$PSScriptRoot/testdata"

            { Test-OrCreatePath -Path "$testDataPath/NonExistentDirectory" } | Should -Not -Throw
            Test-Path "$testDataPath/NonExistentDirectory" | Should -Be $true
            # clean up
            Remove-Item "$testDataPath/NonExistentDirectory"
        }
    }

    It "should throw an exception for an inaccessible path" {
        InModuleScope "Geneva.GitOps.PSModule" {
            { Test-OrCreatePath -Path "//fake-server-name/fake-folder" } | Should -Throw
        }
    }
}

Describe "GetFileAsJson" -Tag "Internal" {
    It "Returns a JSON object when given a valid JSON file path: $testDataPath/file.json" {
        InModuleScope "Geneva.GitOps.PSModule" {
            # InModuleScope, re-define $testDataPath
            $testDataPath = "$PSScriptRoot/testdata"

            $json = GetFileAsJson -Path "$testDataPath/file.json"
            $json | Should -BeOfType 'System.Management.Automation.PSCustomObject'
            $json.bool | Should -Be $true
        }
    }

    It "Throws an error when given an invalid JSON file path: $testDataPath/invalidFile.json" {
        InModuleScope "Geneva.GitOps.PSModule" {
            # InModuleScope, re-define $testDataPath
            $testDataPath = "$PSScriptRoot/testdata"

            { GetFileAsJson -Path "$testDataPath/invalidFile.json" } | Should -Throw
        }
    }

    It "Returns a JSON object when given a valid JSON (with invalid comments) file path: $testDataPath/file-with-comments.json" {
        InModuleScope "Geneva.GitOps.PSModule" {
            # InModuleScope, re-define $testDataPath
            $testDataPath = "$PSScriptRoot/testdata"

            $json = GetFileAsJson -Path "$testDataPath/file-with-comments.json"
            $json | Should -BeOfType 'System.Management.Automation.PSCustomObject'
            $json.a | Should -Be "b"
        }
    }
}

Describe "Get-FilesWithoutDuplicates" -Tag "Internal1" {
    AfterAll {
        # clean up in case a test fails
        Remove-Item -Recurse -Force "${env:TEMP}/Get-FilesWithoutDuplicates-Test" -ErrorAction SilentlyContinue
    }

    Context "When given a valid folder" {
        It "Returns an array with the expected number of files" {
            InModuleScope -ModuleName Geneva.GitOps.PSModule {
                $tempFolder = New-Item -ItemType Directory -Path $env:TEMP -Name "Get-FilesWithoutDuplicates-Test"
                $testFile1 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-1.txt"
                $testFile2 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-2.txt"
                $testFolder = New-Item -ItemType Directory -Path $tempFolder.FullName -Name "test-folder"
                $testSubFile = New-Item -ItemType File -Path $testFolder.FullName -Name "test-sub-file.txt"
  
                $returnFiles = Get-FilesWithoutDuplicates -method "test" -folder $tempFolder.FullName -files @()
  
                $returnFiles.Count | Should -Be 3
  
                # cleanup
                Remove-Item -Recurse -Force $tempFolder.FullName
            }
        }
  
        It "Returns an array with the expected file paths" {
            InModuleScope -ModuleName Geneva.GitOps.PSModule {
                $tempFolder = New-Item -ItemType Directory -Path $env:TEMP -Name "Get-FilesWithoutDuplicates-Test"
                $testFile1 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-1.txt"
                $testFile2 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-2.txt"
                $testFolder = New-Item -ItemType Directory -Path $tempFolder.FullName -Name "test-folder"
                $testSubFile = New-Item -ItemType File -Path $testFolder.FullName -Name "test-sub-file.txt"
  
                $returnFiles = Get-FilesWithoutDuplicates -method "test" -folder $tempFolder.FullName -files @()
  
                $returnFiles | Should -Contain $testFile1.FullName
                $returnFiles | Should -Contain $testFile2.FullName
                $returnFiles | Should -Contain $testSubFile.FullName
  
                Remove-Item -Recurse -Force $tempFolder.FullName
            }
        }
  
        It "Does not write an error message" {
            InModuleScope -ModuleName Geneva.GitOps.PSModule {
                $tempFolder = New-Item -ItemType Directory -Path $env:TEMP -Name "Get-FilesWithoutDuplicates-Test"
                $testFile1 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-1.txt"
                $testFile2 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-2.txt"
                $testFolder = New-Item -ItemType Directory -Path $tempFolder.FullName -Name "test-folder"
                $testSubFile = New-Item -ItemType File -Path $testFolder.FullName -Name "test-sub-file.txt"
  
                { Get-FilesWithoutDuplicates -method "test" -folder $tempFolder.FullName -files @() } | Should -Not -Throw
  
                Remove-Item -Recurse -Force $tempFolder.FullName
            }
        }
    }
  
    Context "When given a folder with a duplicate file" { 
        It "Writes an error message" {
            InModuleScope -ModuleName Geneva.GitOps.PSModule {
                $tempFolder = New-Item -ItemType Directory -Path $env:TEMP -Name "Get-FilesWithoutDuplicates-Test"
                $testFile1 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-1.txt"
                $testFile2 = New-Item -ItemType File -Path $tempFolder.FullName -Name "test-file-2.txt"
                $testFolder = New-Item -ItemType Directory -Path $tempFolder.FullName -Name "test-folder"
                $duplicateFile = New-Item -ItemType File -Path $testFolder.FullName -Name "test-file-1.txt"
  
                { Get-FilesWithoutDuplicates -method "test" -folder $tempFolder.FullName -files @() } | Should -Throw
  
                # Assert that the info message was written
                Should -Invoke -Module "Geneva.GitOps.PSModule" -CommandName Write-HostWithInfo -Times 1 -ParameterFilter { $section -eq "test" -and $message -like "*ERROR: Duplicate file found in source folders: test-file-1.txt in*" }

                Remove-Item -Recurse -Force $tempFolder.FullName -ErrorAction SilentlyContinue
            }
        }
    }

    Context "When given an invalid folder" {
        It "Returns an empty array" {
            InModuleScope -ModuleName Geneva.GitOps.PSModule {
                $returnFiles = Get-FilesWithoutDuplicates -method "test" -folder "invalid-folder" -files @()
  
                $returnFiles.Count | Should -Be 0
            }
        }
  
        It "Writes an error message" {
            InModuleScope -ModuleName Geneva.GitOps.PSModule {
                $returnFiles = Get-FilesWithoutDuplicates -method "test" -folder "invalid-folder" -files @()
  
                Should -Invoke -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "test" -and $message -match "Check your source folder list. Folder not found: invalid-folder" }
            }
        }
    }
}

Describe "Update-Ev2ArtifactsForLogs" {
    BeforeAll {
        $targetArmResourceGroupName = "GenevaLogs"
        $targetArmResourceGroupLocation = "westus2"
        $targetArmSubscriptionId = "68d38d95-0964-447c-8840-f381378f9253"
        $targetServiceResourceGroupName = "ServiceResourceDefinitionLogs"
    }

    BeforeEach {
        Copy-Item -Path "$testDataPath/testRoot1" -Destination "$testDataPath/testRoot1Copy" -Recurse -Force -Container
    }

    It "Happy Path, will create a ScopeBindings.json, RolloutSpec.json, and ServiceModel.json entry for each Logs Account" {
        { Update-Ev2ArtifactsForLogs -SourceRoot "$testDataPath/testRoot1Copy/GenevaSrc" -ServiceGroupRoot "$testDataPath/testRoot1Copy/ServiceGroupRoot" -TargetArmResourceGroupName $targetArmResourceGroupName -TargetArmResourceGroupLocation $targetArmResourceGroupLocation -TargetArmSubscriptionId $targetArmSubscriptionId -TargetServiceResourceGroupName $targetServiceResourceGroupName } | Should -Not -Throw
        
        # check to make sure the ServiceModel is updated
        $serviceModel = GetAsJson -Path "$testDataPath/testRoot1Copy/ServiceGroupRoot/ServiceModel.json"

        $matchedResourceGroup = $serviceModel.ServiceResourceGroups | Where-Object { ($_.AzureResourceGroupName -eq $targetArmResourceGroupName -and $_.Location -eq $targetArmResourceGroupLocation -and $_.AzureSubscriptionId -eq $targetArmSubscriptionId -and $_.InstanceOf -eq $targetServiceResourceGroupName) }

        $matchedResourceGroup | Should -Not -BeNullOrEmpty
        { $matchedResourceGroup.ServiceResources | Where-Object { $_.Name -in "Test_SunnyTestAccount_SunnyEv2Account_Ver1v0" } } | Should -Not -BeNullOrEmpty

        # check Rolloutspec
        $rolloutSpec = GetAsJson -Path "$testDataPath/testRoot1Copy/ServiceGroupRoot/RolloutSpec.json"

        $rolloutSpec.OrchestratedSteps.Length | Should -BeGreaterThan 1
        { $rolloutSpec.OrchestratedSteps | Where-Object { $_.Name -in "Test_SunnyTestAccount_SunnyEv2Account_Ver1v0" } } | Should -Not -BeNullOrEmpty

        # scope bindings
        $scopeBindings = GetAsJson -Path "$testDataPath/testRoot1Copy/ServiceGroupRoot/ScopeBindings.json"
        $scopeBindings.scopeBindings.Length | Should -BeGreaterThan 1
        { $scopeBindings.scopeBindings | Where-Object { $_.Name -in "Test_SunnyTestAccount_SunnyEv2Account_Ver1v0" } } | Should -Not -BeNullOrEmpty
    }

    # TODO Sad Path and Edge cases, this is a complex function with convention needing to be decided
    # for how it handles all of the artifacts, missing, existing, incorrect data, etc.

    AfterEach {
        Remove-Item -Path "$testDataPath/testRoot1Copy" -Recurse -Force
    }
}

Describe "Write-LogsArtifacts" -Tag "Logs" {
    BeforeAll {
        $logsTestPath = "logsRoot"
        $sourceRoot = "$testDataPath/$logsTestPath/GenevaSrc"
        $serviceGroupRoot = "$testDataPath/$logsTestPath/ServiceGroupRoot"
    }

    BeforeEach {
        Copy-Item -Path "$testDataPath/testRoot1" -Destination "$testDataPath/$logsTestPath" -Recurse -Force -Container
    }

    It "Will Create The Appropriate Logs ZIP Artifacts" {
        { Write-LogsArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw

        Test-Path "$testDataPath/$logsTestPath/ServiceGroupRoot/Package/LogsConfig_Test_SunnyTestAccount_SunnyEv2Account_Ver1v0.zip" | Should -Be $true
        Test-Path "$testDataPath/$logsTestPath/ServiceGroupRoot/Package/Logs/Test_SunnyTestAccount_SunnyEv2Account_Ver1v0/main.xml" | Should -Be $true
        Test-Path "$testDataPath/$logsTestPath/ServiceGroupRoot/Package/Logs/Test_SunnyTestAccount_SunnyEv2Account_Ver1v0/imports/AgentStandardEvents.xml" | Should -Be $true

        Test-Path "$testDataPath/$logsTestPath/ServiceGroupRoot/Package/LogsConfig_Test_SunnyTestAccount_SunnyTestAccount_Ver1v0.zip" | Should -Be $true
        Test-Path "$testDataPath/$logsTestPath/ServiceGroupRoot/Package/Logs/Test_SunnyTestAccount_SunnyTestAccount_Ver1v0/main.xml" | Should -Be $true
    }

    AfterEach {
        Remove-Item -Path "$testDataPath/$logsTestPath" -Recurse -Force
    }
}

Describe "Write-MetricsArtifacts" -Tag "Metrics" {
    BeforeAll {
        $metricsTestPath = "metricsRoot"
        $sourceRoot = "$testDataPath/$metricsTestPath/GenevaSrc"
        $serviceGroupRoot = "$testDataPath/$metricsTestPath/ServiceGroupRoot"
    }

    BeforeEach {
        Copy-Item -Path "$testDataPath/testRoot1" -Destination "$testDataPath/$metricsTestPath" -Recurse -Force -Container
    }

    It "Will Create The Appropriate Metrics ZIP Artifact" {
        { Write-MetricsArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw

        Test-Path "$testDataPath/$metricsTestPath/ServiceGroupRoot/Package/MetricConfigs.zip" | Should -Be $true
    }

    It "Will Create The Appropriate Metrics ZIP Artifact when using a custom package path" {
        $configPackagePath = "$testDataPath/$metricsTestPath/ServiceGroupRoot/Package/MyMetricConfigs.zip"

        { Write-MetricsArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -configPackagePath $configPackagePath } | Should -Not -Throw

        Test-Path $configPackagePath | Should -Be $true
    }

    It "Will fail when there are duplicate metric names" {
        $metricsSourceFolders = @("${testDataPath}/DuplicateMetrics")

        { Write-MetricsArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -metricsSourceFolders $metricsSourceFolders } | Should -Throw

        Should -Invoke -ModuleName "Geneva.GitOps.PSModule" -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "Write-MetricsArtifacts" -and $message -like "*ERROR: Duplicate file found in source folders*" }
    }

    It "should throw an error when both files and folders are found" {
        $metricsSourceFolders = @(
            "${testDataPath}/FolderPerNamespaceCheck" # to cause the issue
        )

        { Write-MetricsArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -metricsSourceFolders $metricsSourceFolders } | Should -Throw
            
        Should -Invoke -ModuleName "Geneva.GitOps.PSModule" -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "Write-MetricsArtifacts" -and $message -like "*ERROR: Both files and folders found in*" }
    }

    AfterEach {
        Remove-Item -Path "$testDataPath/$metricsTestPath" -Recurse -Force
    }
}

Describe "Write-HealthArtifacts" -Tag "Health" {
    BeforeAll {
        $healthTestPath = "healthRoot"
        $sourceRoot = "$testDataPath/$healthTestPath/GenevaSrc"
        $serviceGroupRoot = "$testDataPath/$healthTestPath/ServiceGroupRoot"
    }

    BeforeEach {
        Copy-Item -Path "$testDataPath/testRoot1" -Destination "$testDataPath/$healthTestPath" -Recurse -Force -Container
    }

    It "Will Create The Appropriate Health ZIP Artifact" {
        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw

        Test-Path "$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip" | Should -Be $true
    }

    It "Will Create The Appropriate Health ZIP Artifact with a different package path" {
        $configPackagePath = "$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MyHealthMonitorConfigs.zip"

        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -configPackagePath $configPackagePath } | Should -Not -Throw

        Test-Path $configPackagePath | Should -Be $true
    }

    It "should throw an error when both files and folders are found" {
        $healthSourceFolders = @(
            "${testDataPath}/FolderPerNamespaceCheck", # to cause the issue
            "${sourceRoot}/MonitorsV2",
            "${sourceRoot}/HealthMonitors", 
            "${sourceRoot}/TopologyConfig"
        )

        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -healthSourceFolders $healthSourceFolders } | Should -Throw
            
        Should -Invoke -ModuleName "Geneva.GitOps.PSModule" -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "Write-HealthArtifacts" -and $message -like "*ERROR: Both files and folders found in*" }
    }

    It "should throw an error when duplicate files are present" {
        $healthSourceFolders = @(
            "${testDataPath}/DuplicateMonitors" # duplicate monitor file names to cause issue
        )

        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -healthSourceFolders $healthSourceFolders } | Should -Throw
            
        Should -Invoke -ModuleName "Geneva.GitOps.PSModule" -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "Write-HealthArtifacts" -and $message -like "*ERROR: Duplicate file found in source folders*" }
    }

    It "will flatten folder structures for artifacts with folder per namespace enabled" {
        $healthSourceFolders = @(
            "${testDataPath}/FolderPerNamespaceValid",
            "${sourceRoot}/Monitors",
            "${sourceRoot}/MonitorsV2"
        )

        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -healthSourceFolders $healthSourceFolders } | Should -Not -Throw

        # zip should exist
        Test-Path "$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip" | Should -Be $true
        # manually open the zip and check the contents
        $zip = [System.IO.Compression.ZipFile]::OpenRead("$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip")
        $zip | Should -Not -BeNullOrEmpty
        # make a copy of the names so we can close the zip file before running assertions
        $names = $zip.Entries | Select-Object -ExpandProperty FullName
        # close the zip file
        $zip.Dispose()
        # files should be present in the appropriate folder
        # and they should be the only entries
        $names.Count | Should -Be 4
        $names | Where-Object { $_ -eq "FolderPerNamespaceValid\monitor1.json" -or $_ -eq "FolderPerNamespaceValid/monitor1.json" } | Should -Not -BeNullOrEmpty
        $names | Where-Object { $_ -eq "FolderPerNamespaceValid\monitor2.json" -or $_ -eq "FolderPerNamespaceValid/monitor2.json" } | Should -Not -BeNullOrEmpty
        $names | Where-Object { $_ -eq "Monitors\SunnyTestAccount_WarmPathQoS_RequestCount.json" -or $_ -eq "Monitors/SunnyTestAccount_WarmPathQoS_RequestCount.json" } | Should -Not -BeNullOrEmpty        
        $names | Where-Object { $_ -eq "MonitorsV2\b9604ab5-93b0-44f4-be8c-31693b1bc973.json" -or $_ -eq "MonitorsV2/b9604ab5-93b0-44f4-be8c-31693b1bc973.json" } | Should -Not -BeNullOrEmpty        
    }

    It "will merge JavaScript snippets into the V1 Monitor" {
        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw

        # zip should exist
        Test-Path "$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip" | Should -Be $true
        # JS snippet message for V1 should be written
        Should -Invoke -ModuleName "Geneva.GitOps.PSModule" -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "Write-HealthArtifacts" -and $message -like "*Monitor V1 has JsFileRef embedded*" }        
        # manually open the zip and check the contents
        $zip = [System.IO.Compression.ZipFile]::OpenRead("$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip")
        $zip | Should -Not -BeNullOrEmpty
        # get the file out of the zip
        $entry = $zip.Entries | Where-Object { $_.Name -eq "SunnyTestAccount_WarmPathQoS_RequestCount.json" }
        $entry | Should -Not -BeNullOrEmpty
        # convert from JSON
        $stream = $entry.Open()
        # create a stream reader for the stream
        $reader = New-Object System.IO.StreamReader($stream)
        # read the stream
        $content = $reader.ReadToEnd()
        # convert the content to JSON
        $json = $content | ConvertFrom-Json
        # close the reader
        $reader.Close()
        # close the stream
        $stream.Close()
        # close the zip file
        $zip.Dispose()
        # check that the monitors length is greater than 0
        $json.monitors.Length | Should -BeGreaterThan 0
        # check that at least one jsSnippet contains EXACTLY "var x = 1;"
        $json.monitors | Where-Object -FilterScript { $_.templateSpecificParameters.jsSnippet -match "var x = 1;" } | Should -Not -BeNullOrEmpty
    }

    It "will merge JavaScript snippets into the V2 Monitor" {
        { Write-HealthArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw

        # zip should exist
        Test-Path "$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip" | Should -Be $true
        # JS snippet message for V2 should be written
        Should -Invoke -ModuleName "Geneva.GitOps.PSModule" -CommandName "Write-HostWithInfo" -Times 1 -ParameterFilter { $Section -eq "Write-HealthArtifacts" -and $message -like "*Monitor V2 has JsFileRef embedded*" }        
        # manually open the zip and check the contents
        $zip = [System.IO.Compression.ZipFile]::OpenRead("$testDataPath/$healthTestPath/ServiceGroupRoot/Package/MonitorConfigs.zip")
        $zip | Should -Not -BeNullOrEmpty
        # get the file out of the zip
        $entry = $zip.Entries | Where-Object { $_.Name -eq "b9604ab5-93b0-44f4-be8c-31693b1bc973.json" }
        $entry | Should -Not -BeNullOrEmpty
        # convert from JSON
        $stream = $entry.Open()
        # create a stream reader for the stream
        $reader = New-Object System.IO.StreamReader($stream)
        # read the stream
        $content = $reader.ReadToEnd()
        # convert the content to JSON
        $json = $content | ConvertFrom-Json
        # close the reader
        $reader.Close()
        # close the stream
        $stream.Close()
        # close the zip file
        $zip.Dispose()
        # check that the alertConditions length is greater than 0
        $json.alertConditions.Length | Should -BeGreaterThan 0
        # check that at least one snippet contains EXACTLY "var x2 = 1;"
        $json | Where-Object -FilterScript { $_.alertConditions.conditions | Where-Object -FilterScript { $_.snippet -match "var x2 = 1;" } } | Should -Not -BeNullOrEmpty
    }

    AfterEach {
        Remove-Item -Path "$testDataPath/$healthTestPath" -Recurse -Force
    }
}

Describe "Write-SloArtifacts" -Tag "Slo" {
    BeforeAll {
        $sloTestPath = "sloRoot"
        $sourceRoot = "$testDataPath/$sloTestPath/GenevaSrc/"
        $serviceGroupRoot = "$testDataPath/$sloTestPath/ServiceGroupRoot/"
        $packagesRoot = "${serviceGroupRoot}/Package/"
        $sloSourceFolders = @("${sourceRoot}/SLO_SLI/")
        $packageDestinationFolder = "${packagesRoot}/SLOConfigs/"
    }

    BeforeEach {
        Copy-Item -Path "$testDataPath/testRoot1" -Destination "$testDataPath/$sloTestPath" -Recurse -Force -Container
    }

    Context "With minimum parameters" {
        It "Creates the expected directories" {     
            { Write-SloArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw
  
            # Assert that the expected directories were created
            Test-Path $packagesRoot | Should -Be $true
            Test-Path $packageDestinationFolder | Should -Be $true
        }
    }
  
    # Test the function with non-default parameters
    Context "With non-default parameters" {
        It "Creates the expected directories and copies the expected files" {
            # point towards the test data
            $sloSourceFolders = @("${testDataPath}/SLOFiles")
 
            { Write-SloArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -packagesRoot $packagesRoot -sloSourceFolders $sloSourceFolders -packageDestinationFolder $packageDestinationFolder } | Should -Not -Throw
  
            # Assert that the expected directories were created
            Test-Path $packagesRoot | Should -Be $true
            Test-Path $packageDestinationFolder | Should -Be $true
  
            # Assert that the expected files were copied
            Test-Path "${packageDestinationFolder}/empty1.yaml" | Should -Be $true
            Test-Path "${packageDestinationFolder}/empty2.yaml" | Should -Be $true
        }
    }
  
    # Test the function with duplicate files
    Context "With duplicate files" {
        It "Writes an error message" {
            # ensure duplicate files
            $sloSourceFolders = @("${testDataPath}/SLOFiles", "${testDataPath}/SLOFiles")

            { Write-SloArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -packagesRoot $packagesRoot -sloSourceFolders $sloSourceFolders -packageDestinationFolder $packageDestinationFolder } | Should -Throw
        }
    }
  
    # Test the function with no SLO/SLI configurations
    Context "With no SLO/SLI configurations" {
        It "Writes an info message" {
            $sloSourceFolders = @("${sourceRoot}/empty-folder/")
  
            Write-SloArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -packagesRoot $packagesRoot -sloSourceFolders $sloSourceFolders -packageDestinationFolder $packageDestinationFolder
  
            # Assert that the info message was written
            Should -Invoke -Module "Geneva.GitOps.PSModule" -CommandName Write-HostWithInfo -Times 1 -ParameterFilter { $section -eq "Write-SloArtifacts" -and $message -match "No SLO/SLI configurations found, you may pass an array of folder names using the sloSourceFolders parameter" }
  
            # Assert that the expected directories were created
            Test-Path $packagesRoot | Should -Be $true
            Test-Path $packageDestinationFolder | Should -Be $true
        }
    }

    AfterEach {
        Remove-Item -Path "$testDataPath/$sloTestPath" -Recurse -Force
    }
}

Describe "Write-DashboardArtifacts" -Tag "Dashboards" {
    BeforeAll {
        $dashboardsTestPath = "testRootDashboards"
        $sourceRoot = "$testDataPath/$dashboardsTestPath/GenevaSrc"
        $serviceGroupRoot = "$testDataPath/$dashboardsTestPath/ServiceGroupRoot"
    }

    BeforeEach {
        Copy-Item -Path "$testDataPath/testRoot1" -Destination "$testDataPath/$dashboardsTestPath" -Recurse -Force -Container
    }

    It "Will Create The Appropriate Dashboards ZIP Artifact" {
        { Write-DashboardArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot } | Should -Not -Throw

        Test-Path "$testDataPath/$dashboardsTestPath/ServiceGroupRoot/Package/Dashboards.zip" | Should -Be $true
    }

    It "Will Create The Appropriate Dashboards ZIP Artifact when using a custom package path" {
        $configPackagePath = "$testDataPath/$dashboardsTestPath/ServiceGroupRoot/Package/MyDashboards.zip"

        { Write-DashboardArtifacts -sourceRoot $sourceRoot -serviceGroupRoot $serviceGroupRoot -configPackagePath $configPackagePath } | Should -Not -Throw

        Test-Path $configPackagePath | Should -Be $true
    }

    AfterEach {
        Remove-Item -Path "$testDataPath/$dashboardsTestPath" -Recurse -Force
    }
}