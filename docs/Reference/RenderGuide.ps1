function GuideId([string] $path) {
    'guide-' + ($path -replace '[^a-zA-Z0-9]+', '-').ToLowerInvariant()
}

function OutputLink([string] $page, [string] $target) {
    [IO.Path]::GetRelativePath((Split-Path (Join-Path $docs $page)), $target).Replace('\', '/')
}

function RenderGuide($file, [string] $page) {
    $id = GuideId (RelativePath $file)
    $html = [Markdig.Markdown]::ToHtml([IO.File]::ReadAllText($file.FullName), $markdownPipeline)
    $html = [regex]::Replace($html, '\bid="([^"]+)"', { param($match) "id='$id-$($match.Groups[1].Value)'" })
    $html = [regex]::Replace($html, '\b(href|src)="([^"]+)"', {
        param($match)
        $attribute = $match.Groups[1].Value
        $link = [Net.WebUtility]::HtmlDecode($match.Groups[2].Value)
        if ($link -match '^(?:[a-z][a-z\d+.-]*:|/)') { return $match.Value }
        $target, $anchor = $link.Split('#', 2)
        if (!$target) { return "$attribute='#$id-$(Encode $anchor)'" }

        $absolute = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName ([uri]::UnescapeDataString($target))))
        if (!(Test-Path -LiteralPath $absolute)) { return "$(Encode $target)" }
        if ($guideFiles.ContainsKey($absolute)) {
            $guide = $guideFiles[$absolute]
            $url = OutputLink $page (Join-Path $docs 'Reference/guides.html')
            $url += '#' + (GuideId (RelativePath $guide))
            if ($anchor) { $url += "-$anchor" }
        } else {
            $url = OutputLink $page $absolute
            if ($anchor) { $url += "#$anchor" }
        }
        "$attribute='$(Encode $url)'"
    })
    "<section class='guide' id='$id'>$html</section>"
}
# Load PowerShell's bundled Markdown parser and use GitHub-compatible heading IDs.
$null = ConvertFrom-Markdown -InputObject 'Documentation'
$markdownBuilder = [Markdig.MarkdownPipelineBuilder]::new()
$null = [Markdig.MarkdownExtensions]::UseAutoIdentifiers(
    $markdownBuilder, [Markdig.Extensions.AutoIdentifiers.AutoIdentifierOptions]::GitHub)
$null = [Markdig.MarkdownExtensions]::UsePipeTables($markdownBuilder, $null)
$markdownPipeline = $markdownBuilder.Build()

