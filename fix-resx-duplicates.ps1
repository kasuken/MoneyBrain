##
# Remove duplicate resource entries from .resx files
##

$resxFiles = @(
    "MoneyBrain.Web\MoneyBrain.Web\Resources\SharedResource.resx",
    "MoneyBrain.Web\MoneyBrain.Web\Resources\SharedResource.de.resx",
    "MoneyBrain.Web\MoneyBrain.Web\Resources\SharedResource.es.resx",
    "MoneyBrain.Web\MoneyBrain.Web\Resources\SharedResource.it.resx"
)

foreach ($file in $resxFiles) {
    $fullPath = Join-Path $PSScriptRoot $file
    
    if (-not (Test-Path $fullPath)) {
        Write-Warning "File not found: $fullPath"
        continue
    }
    
    Write-Host "`nProcessing: $file" -ForegroundColor Cyan
    
    # Load XML
    [xml]$xml = Get-Content $fullPath -Raw
    
    # Track seen resource names and duplicates found
    $seenNames = @{}
    $duplicatesToRemove = @()
    
    # Find all <data> elements
    $dataElements = $xml.root.data
    
    foreach ($data in $dataElements) {
        $name = $data.name
        
        if ($seenNames.ContainsKey($name)) {
            # Duplicate found
            Write-Host "  Found duplicate: $name" -ForegroundColor Yellow
            $duplicatesToRemove += $data
        }
        else {
            $seenNames[$name] = $true
        }
    }
    
    # Remove duplicates
    if ($duplicatesToRemove.Count -gt 0) {
        Write-Host "  Removing $($duplicatesToRemove.Count) duplicate(s)..." -ForegroundColor Green
        
        foreach ($duplicate in $duplicatesToRemove) {
            $xml.root.RemoveChild($duplicate) | Out-Null
        }
        
        # Save with proper formatting
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Indent = $true
        $settings.IndentChars = "  "
        $settings.NewLineChars = "`r`n"
        $settings.Encoding = [System.Text.UTF8Encoding]::new($false) # UTF-8 without BOM
        
        $writer = [System.Xml.XmlWriter]::Create($fullPath, $settings)
        try {
            $xml.Save($writer)
            Write-Host "  Saved: $file" -ForegroundColor Green
        }
        finally {
            $writer.Close()
        }
    }
    else {
        Write-Host "  No duplicates found" -ForegroundColor Gray
    }
}

Write-Host "`nDone! Run 'dotnet build' to verify warnings are resolved." -ForegroundColor Green
