<#
    .SYNOPSIS
    Adds the class library and NuGet projects necessary to support a new native library
    to the solution.

    .PARAMETER LibraryName
    The name of the library you are introducing support for, without the .dll extension.

    .EXAMPLE
    AddNewLibrary -LibraryName BCrypt
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true,Position=0)]
    [string]$LibraryName
)
function Replace-Placeholders {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        [Parameter(Mandatory=$true)]
        [string]$LibraryName,
        [Parameter(Mandatory=$true)]
        $Replacements
    )

    Get-ChildItem -Recurse $Path -File |% {
        $content = Get-Content -Path $_.FullName
        $Replacements.GetEnumerator() |% { $content = $content -replace $_.Key,$_.Value }
        Set-Content -Path $_.FullName -Value $content -Encoding UTF8

        if ($_.Name -match 'LIBNAME') {
            $NewLeafName = $_.Name -Replace 'LIBNAME',$LibraryName
            Rename-Item $_.FullName $NewLeafName
        }
    }

    # Rename directories
    Get-ChildItem -Recurse $Path -Directory |% {
        if ($_.Name -match 'LIBNAME') {
            $NewLeafName = $_.Name -Replace 'LIBNAME',$LibraryName
            Rename-Item $_.FullName $NewLeafName
        }
    }

    # Finally, rename $Path itself
    $NewLeafName = (Split-Path -Leaf $Path) -Replace 'LIBNAME',$LibraryName
    Rename-Item $Path $NewLeafName
}

$Src = Resolve-Path "$PSScriptRoot\..\src"

$Directories = 'LIBNAME','LIBNAME.Desktop','LIBNAME.Shared','LIBNAME.Tests','LIBNAME.NuGet'
$TemplateDirectories = $Directories |% { "$PSScriptRoot\$_" }
$SrcDirectories = $Directories |% { "$Src\$_" }

$Replacements = @{
    '\$guid1\$' = [Guid]::NewGuid().ToString('b').ToUpper();
    '\$guid2\$' = [Guid]::NewGuid().ToString('b').ToUpper();
    '\$guid3\$' = [Guid]::NewGuid().ToString('b').ToUpper();
    '\$guid4\$' = [Guid]::NewGuid().ToString('b').ToUpper();
    '\$guid5\$' = [Guid]::NewGuid().ToString('b').ToUpper();
    'LIBNAME' = $LibraryName;
}

Copy-Item -Recurse -Path $TemplateDirectories -Destination $Src
$SrcDirectories |% { Replace-Placeholders -LibraryName $LibraryName -Replacements $Replacements -Path $_ }

& "$PSScriptRoot\CreateExportsTxtFile.ps1" -AssemblyPath "$env:windir\System32\$LibraryName.dll" -OutputDir "$Src\$LibraryName.Desktop\"

Write-Output "Great. Your new projects have been created."
Write-Output "Please add these new projects to your solution file:"
Write-Output "    $Src\$LibraryName\$LibraryName.csproj"
Write-Output "    $Src\$LibraryName.Desktop\$LibraryName.Desktop.csproj"
Write-Output "    $Src\$LibraryName.Shared\$LibraryName.Shared.shproj"
Write-Output "    $Src\$LibraryName.Tests\$LibraryName.Tests.csproj"
Write-Output "    $Src\$LibraryName.NuGet\$LibraryName.NuGet.nuproj"
Write-Output "Also please add your library to the README.md file."
