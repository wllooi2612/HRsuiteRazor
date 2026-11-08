$webPagesDir = "c:\HRsuiteRazor\Components\Pages"
$mobilePagesDir = "c:\HRsuiteRazor\HRsuiteRazor.Mobile\Components\Pages"

$pages = @(
    "AddNewUserQde.razor",
    "GroupSetup.razor",
    "GroupPageSetup.razor",
    "UserManage.razor",
    "Admin.razor"
)

foreach ($p in $pages) {
    $src = Join-Path $webPagesDir $p
    $dest = Join-Path $mobilePagesDir $p
    if (Test-Path $src) {
        $content = [System.IO.File]::ReadAllText($src)
        $content = $content -replace '@rendermode\s+InteractiveServer\r?\n', ''
        [System.IO.File]::WriteAllText($dest, $content)
        Write-Host "Synced $p -> Mobile"
    }
}
