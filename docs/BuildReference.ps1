# Run with PowerShell 7 from the package root: pwsh -File docs/BuildReference.ps1
$ErrorActionPreference = 'Stop'
$package = Split-Path $PSScriptRoot
# Initialize PowerShell's bundled Roslyn compiler.
Add-Type -TypeDefinition 'public class QuietStaticDocumentationCompiler {}'

function Encode([string] $value) {
    [Net.WebUtility]::HtmlEncode($value)
}

function RelativePath($file) {
    [IO.Path]::GetRelativePath($package, $file.FullName).Replace('\', '/')
}

function IsType($node) {
    $node.GetType().Name -match '^(Class|Struct|Record|Interface|Enum|Delegate)DeclarationSyntax$'
}

function IsPublic($node) {
    $modifiers = @($node.Modifiers | ForEach-Object ValueText)
    ($modifiers -contains 'public') -or
        ($node.GetType().Name -eq 'EnumMemberDeclarationSyntax') -or
        ($node.Parent.GetType().Name -eq 'InterfaceDeclarationSyntax' -and
            !($modifiers | Where-Object { $_ -in 'private','protected','internal' }))
}

function Signature($node) {
    $start = $node.Span.Start
    if ($node.AttributeLists.Count) { $start = $node.AttributeLists.Last().Span.End }
    $end = $node.Span.End
    foreach ($part in $node.ChildNodesAndTokens()) {
        if ([Microsoft.CodeAnalysis.CSharp.SyntaxKind]$part.RawKind -in
            'Block','ArrowExpressionClause','AccessorList','OpenBraceToken') {
            $end = $part.Span.Start
            break
        }
    }
    $text = $node.ToFullString().Substring($start - $node.FullSpan.Start, $end - $start)
    $text = ($text -replace '\s+', ' ').Trim().TrimEnd(';')
    if ($node.AccessorList) {
        $accessors = foreach ($accessor in $node.AccessorList.Accessors) {
            "$($accessor.Modifiers) $($accessor.Keyword);".Trim()
        }
        $text += ' { ' + ($accessors -join ' ') + ' }'
    } elseif ($node.ExpressionBody -and $node.GetType().Name -in 'PropertyDeclarationSyntax','IndexerDeclarationSyntax') {
        $text += ' { get; }'
    }
    "<code class='signature'>$(Encode $text)</code>"
}

function DocComment($node) {
    $xml = (($node.GetLeadingTrivia().ToFullString() -split '\r?\n') |
        Where-Object { $_ -match '^\s*///' } | ForEach-Object { $_ -replace '^\s*/// ?', '' }) -join [char]10
    if (!$xml) { return }
    try { $doc = [Xml.Linq.XElement]::Parse("<doc>$xml</doc>") }
    catch { return } # Keep indexing if an existing source comment contains invalid XML.
    foreach ($inline in @($doc.Descendants())) {
        if ($inline.Name.LocalName -in 'see','paramref','typeparamref') {
            $label = $inline.Attribute('cref') ?? $inline.Attribute('langword') ?? $inline.Attribute('name')
            if ($label) { $inline.ReplaceAll([string]$label.Value) }
        }
    }
    foreach ($section in $doc.Elements()) {
        $kind = $section.Name.LocalName
        $label = switch ($kind) {
            { $_ -in 'param','typeparam' } { $section.Attribute('name').Value }
            'exception' { "Throws $($section.Attribute('cref').Value)" }
            { $_ -in 'remarks','returns','value' } { (Get-Culture).TextInfo.ToTitleCase($_) }
        }
        if ($kind -eq 'example') {
            "<pre>$(Encode $section.Value.Trim())</pre>"
        } elseif ($kind -in 'summary','remarks','param','typeparam','returns','value','exception') {
            $text = Encode (($section.Value -replace '\s+', ' ').Trim())
            if ($label) { $text = "<strong>$(Encode $label)</strong> $text" }
            if ($text) { "<p class='comment'>$text</p>" }
        }
    }
}

$options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Preview).WithPreprocessorSymbols([string[]]@('UNITY_EDITOR', 'UNITY_6000_3_OR_NEWER'))
$files = Get-ChildItem -LiteralPath (Join-Path $package 'Runtime'), (Join-Path $package 'Editor') -Recurse -File
$types = foreach ($file in $files | Where-Object Extension -eq '.cs' | Sort-Object FullName) {
    $path = RelativePath $file
    $parts = $path.Split('/')
    $module = if ($parts.Count -gt 2) { "$($parts[0]) / $($parts[1])" } else { $parts[0] }
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName), $options, $path)
    foreach ($node in $tree.GetRoot().DescendantNodes()) {
        if (!(IsType $node) -or !(IsPublic $node)) { continue }
        $names = [Collections.Generic.List[string]]::new()
        for ($ancestor = $node; $ancestor; $ancestor = $ancestor.Parent) {
            if ($ancestor -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax]) {
                $names.Insert(0, $ancestor.Name.ToString())
            } elseif (IsType $ancestor) {
                if (!(IsPublic $ancestor)) { break }
                $name = $ancestor.Identifier.ValueText
                if ($ancestor.TypeParameterList) { $name += '-' + $ancestor.TypeParameterList.Parameters.Count }
                $names.Insert(0, $name)
            }
        }
        if ($ancestor) { continue }
        [pscustomobject]@{ Module = $module; Name = $node.Identifier.ValueText; QualifiedName = $names -join '.'; Path = $path; Node = $node }
    }
}

$code = [Collections.Generic.List[string]]::new()
$code.AddRange([string[]]@(
    '<h1>Code reference</h1>'
    '<p>Unity 6.3 runtime and editor APIs for <code>com.quietstatic.core</code>. Browse by package folder or search by type, member, or namespace. Expand a type to see its declaration, public members, and available source comments.</p>'
    '<h2>Getting started</h2>'
    '<ol><li>In Unity, choose <strong>Window &gt; Package Management &gt; Package Manager</strong> and install the package from <code>https://github.com/quiet-static/library.git</code>.</li><li>Choose <strong>Tools &gt; Quiet Static &gt; Samples &gt; Import and Open Executable Example</strong> to run the sample.</li><li>See the <a href="README.md">setup guides</a> and <a href="Runtime/CommonComponentRecipes.md">component recipes</a> for scene composition and common workflows.</li></ol>'
))
$codeIndex = foreach ($module in $types | Group-Object Module) {
    $id = 'module-' + ($module.Name -replace ' / ', '-').ToLowerInvariant()
    $label = Encode $module.Name
    "<li><a href='#$id'>$label</a></li>"
    $code.Add("<section class='catalog' id='$id'><h2>$label</h2>")
    if ($module.Name -match '^Runtime / (\w+)$' -and (Test-Path (Join-Path $package "Runtime/$($Matches[1])/README.md"))) {
        $code.Add("<p><a href='../Runtime/$($Matches[1])/README.md'>Module setup guide</a></p>")
    }
    foreach ($group in $module.Group | Group-Object QualifiedName | Sort-Object { $_.Group[0].Name }) {
        $type = $group.Group[0]
        $name = Encode $type.Name
        $qualified = Encode $type.QualifiedName
        $code.Add("<details data-entry id='api-$qualified'><summary><code>$name</code> <span class='source'>$qualified</span></summary><div class='entry'>")
        foreach ($part in $group.Group) {
            $path = Encode $part.Path
            $code.Add((Signature $part.Node))
            $code.Add("<p class='source'><a href='../$path'>$path</a> · <a href='#api-$qualified'>Permalink</a></p>")
            $code.AddRange([string[]]@(DocComment $part.Node))
            foreach ($member in $part.Node.Members | Where-Object { (IsPublic $_) -and !(IsType $_) }) {
                $code.Add("<div class='member'>$(Signature $member)")
                $code.AddRange([string[]]@(DocComment $member))
                $code.Add('</div>')
            }
        }
        $code.Add('</div></details>')
    }
    $code.Add('</section>')
}

$assets = [Collections.Generic.List[string]]::new()
$assets.Add('<h1>Asset reference</h1>')
$assets.Add('<p>Browse the prefabs, ScriptableObject instances, scenes, and artwork shipped with the package. Find runtime assets under <strong>Packages &gt; Quiet Static Library</strong> in Unity&#39;s Project window. Import sample assets from the package&#39;s <strong>Samples</strong> tab.</p>')
$sampleNotes = @{
    ExecutableToolkitExample = 'Open the bootstrap scene through <strong>Tools &gt; Quiet Static &gt; Samples &gt; Import and Open Executable Example</strong>. The example loads Systems and Content to demonstrate a flag-driven objective.'
    CompositionReferences = 'Inspect these scenes for reusable prefab, narrative, and horror compositions. Adapt them into your own project before play.'
}
$assetNotes = @{
    SystemManagers = 'Persistent settings and pause services for the Systems scene.'
    GameplayManagers = 'Shared gameplay state and progression services. Assign the databases and channels for your project.'
    AudioManagers = 'Shared audio services for the persistent Systems scene.'
    UIControllers = 'Place in a persistent UI scene to keep common UI controllers available during content transitions.'
    TitleMenu = 'A starting point for a title screen. Create a prefab variant for game-specific presentation.'
    PauseMenu = 'A pause screen with settings UI. Use with the persistent pause and settings services.'
    SettingsMenu = 'A standalone settings view for a title or pause menu.'
    ProgressBarTheme = 'Shared styling for world-space progress bars and screen-space sliders.'
    BasicInteractable = 'Start here for a simple interaction and wire the action in the Inspector.'
    HoldInteractable = 'Use for an interaction that requires the player to hold the input until progress completes.'
    ActivatedProgressInteractable = 'Use when an interaction starts a timed activity that then advances on its own.'
    DialogueInteractable = 'Use to launch dialogue from a scene interaction.'
    ConditionalMessageInteractable = 'Use to change the prompt or response according to conditions.'
    FirstPersonPlayer = 'First-person player prefab. Pair it with the first-person camera rig.'
    Player = 'Third-person player prefab. Pair it with the third-person camera rig.'
    QuietStaticSampleBootstrap = 'Entry scene for the runnable sample.'
    QuietStaticSampleSystems = 'Persistent services and UI for the runnable sample.'
    QuietStaticSampleContent = 'Content scene with the interaction and flag-driven objective.'
}
$assetKinds = @{ '.prefab' = 'Prefab'; '.asset' = 'ScriptableObject asset'; '.unity' = 'Scene'; '.png' = 'Texture' }
$catalog = @($files) + @(Get-ChildItem -LiteralPath (Join-Path $package 'Samples~') -Recurse -File)
$scriptNames = @{}
foreach ($file in $files | Where-Object Name -like '*.cs.meta') {
    if ((Get-Content -LiteralPath $file.FullName -Raw) -match '(?m)^guid: (\w+)') {
        $scriptNames[$Matches[1]] = $file.Name -replace '\.cs\.meta$', ''
    }
}

$assetIndex = foreach ($module in $catalog | Where-Object { $assetKinds.ContainsKey($_.Extension) } | Group-Object {
    $parts = (RelativePath $_).Split('/')
    "$($parts[0])/$($parts[1])"
}) {
    $area, $folder = $module.Name.Split('/')
    $id = 'assets-' + ($module.Name -replace '[^\w]+', '-').ToLowerInvariant()
    $label = switch ($folder) {
        'ExecutableToolkitExample' { 'Executable Toolkit Example' }
        'CompositionReferences' { 'Composition References' }
        default { Encode $folder }
    }
    "<li><a href='#$id'>$label</a></li>"
    $assets.Add("<section class='catalog' id='$id'><h2>$label</h2>")
    if ($sampleNotes.ContainsKey($folder)) {
        $assets.Add("<p>$($sampleNotes[$folder]) <a href='../Samples~/$folder/README.md'>Sample guide</a></p>")
    } elseif (Test-Path -LiteralPath (Join-Path $package "$area/$folder/README.md")) {
        $assets.Add("<p><a href='../$area/$folder/README.md'>Module setup guide</a></p>")
    }
    foreach ($asset in $module.Group | Sort-Object BaseName) {
        $rawPath = RelativePath $asset
        $path = Encode $rawPath
        $name = Encode $asset.BaseName
        $kind = $assetKinds[$asset.Extension]
        $anchor = 'asset-' + ($rawPath -replace '[^\w]+', '-')
        $assets.Add("<article class='asset' data-entry id='$anchor'><h3>$name</h3><p>$kind</p>")
        if ($assetNotes.ContainsKey($asset.BaseName)) {
            $assets.Add("<p>$(Encode $assetNotes[$asset.BaseName])</p>")
        }
        if ($asset.Extension -eq '.png') {
            $assets.Add("<img src='../$path' alt='$name texture'>")
        } else {
            $references = [regex]::Matches([IO.File]::ReadAllText($asset.FullName), 'm_Script: \{[^\r\n]*?guid: (\w+)')
            $scripts = @($references | ForEach-Object { $scriptNames[$_.Groups[1].Value] } | Where-Object { $_ } | Sort-Object -Unique)
            if ($scripts.Count) {
                $links = foreach ($script in $scripts) {
                    $targets = @($types | Where-Object Name -eq $script | Select-Object -ExpandProperty QualifiedName -Unique)
                    $script = Encode $script
                    if ($targets.Count -eq 1) { "<a href='index.html#api-$(Encode $targets[0])'><code>$script</code></a>" }
                    else { "<code>$script</code>" }
                }
                $assets.Add("<p>QS scripts $($links -join ', ')</p>")
            }
        }
        $assets.Add("<p class='source'><a href='../$path'>$path</a> · <a href='#$anchor'>Permalink</a></p></article>")
    }
    $assets.Add('</section>')
}

$style = @'
<style>
  :root { color-scheme: light; --ink: #193245; --blue: #205777; --muted: #486171; --line: #ccdae2; --wash: #eef4f7; }
  * { box-sizing: border-box; }
  body { margin: 0; font: 1rem/1.55 system-ui, sans-serif; color: var(--ink); background: var(--wash); }
  a { color: var(--blue); text-underline-offset: .18em; }
  a:hover { text-decoration-thickness: 2px; }
  :focus-visible { outline: 3px solid #ad710e; outline-offset: 3px; }
  .wrap { max-width: 82rem; margin: auto; padding-inline: clamp(1rem, 4vw, 3rem); }
  header { background: #16415c; color: white; }
  header p { margin: 0; padding-block: 1rem .65rem; font-size: 1.15rem; font-weight: 650; }
  header small { margin-left: .65rem; color: #d1e4ef; font-weight: 400; }
  nav ul { display: flex; gap: .35rem; margin: 0; padding: 0; list-style: none; }
  nav a { display: block; padding: .55rem 1.2rem; color: white; }
  nav a[aria-current] { background: var(--wash); color: var(--ink); font-weight: 650; text-decoration: none; border-radius: .4rem .4rem 0 0; }
  .layout { display: grid; grid-template-columns: minmax(12rem, 16rem) minmax(0, 1fr); gap: clamp(1rem, 3vw, 2.5rem); padding-block: 1.5rem 3rem; }
  main { min-width: 0; background: white; border: 1px solid var(--line); padding: clamp(1rem, 4vw, 2.5rem); }
  h1, h2, h3 { line-height: 1.2; }
  h1 { margin-top: 0; font-size: clamp(1.85rem, 4vw, 2.6rem); }
  h2 { margin-top: 2rem; border-bottom: 1px solid var(--line); padding-bottom: .35rem; }
  aside h2 { font-size: 1rem; margin: 1.2rem 0 .45rem; }
  aside ul { padding-left: 1.25rem; margin-top: .25rem; }
  aside li { margin-block: .18rem; }
  input { width: 100%; padding: .55rem .65rem; background: white; border: 1px solid #7c96a6; border-radius: .25rem; font: inherit; }
  label { display: block; font-weight: 650; margin-bottom: .3rem; }
  details, .asset { margin: .65rem 0; border: 1px solid var(--line); border-radius: .25rem; }
  summary { padding: .6rem .8rem; background: var(--wash); cursor: pointer; overflow-wrap: anywhere; }
  details[open] summary { border-bottom: 1px solid var(--line); }
  .entry, .asset { padding: .75rem .85rem; }
  .asset h3 { margin: 0 0 .3rem; font-size: 1.05rem; overflow-wrap: anywhere; }
  .asset p, .source { margin-block: .35rem; }
  .source, #result { color: var(--muted); }
  .source { font-size: .9rem; overflow-wrap: anywhere; }
  .member { margin-block: .75rem; border-top: 1px solid var(--line); padding-top: .75rem; }
  .signature { display: block; padding: .4rem .55rem; background: var(--wash); overflow-wrap: anywhere; }
  code, pre { font-family: ui-monospace, Consolas, monospace; }
  pre { overflow: auto; }
  section, [data-entry] { scroll-margin-block: 1rem; }
  [data-entry]:target { outline: 2px solid #ad710e; outline-offset: 2px; }
  .asset img { width: 3rem; padding: .35rem; background: var(--blue); border-radius: .25rem; }
  [hidden] { display: none !important; }
  @media (max-width: 48rem) { .layout { grid-template-columns: minmax(0, 1fr); } aside ul { columns: 2; } }
</style>
'@
$client = @'
<script>
  const search = document.querySelector('#search');
  const sections = Array.from(document.querySelectorAll('.catalog')).map(section => ({
    section,
    link: document.querySelector('a[href="#' + section.id + '"]').closest('li'),
    items: Array.from(section.querySelectorAll('[data-entry]')).map(entry => ({
      entry,
      text: entry.textContent.toLowerCase()
    }))
  }));
  search.addEventListener('input', () => {
    const words = search.value.trim().toLowerCase().split(/\s+/).filter(Boolean);
    let count = 0;
    for (const { section, link, items } of sections) {
      let matches = 0;
      for (const { entry, text } of items) {
        entry.hidden = !words.every(word => text.includes(word));
        if (!entry.hidden) matches++;
      }
      section.hidden = matches === 0;
      link.hidden = section.hidden;
      count += matches;
    }
    document.querySelector('#result').textContent = words.length
      ? (count ? count + (count === 1 ? ' match' : ' matches') : 'No matches.')
      : '';
  });

  function revealTarget() {
    let id;
    try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
    const target = document.getElementById(id);
    if (!target) return;
    if (target.closest('[hidden]')) {
      search.value = '';
      search.dispatchEvent(new Event('input'));
    }
    const details = target.closest('details');
    if (details) details.open = true;
    target.scrollIntoView();
  }

  window.addEventListener('hashchange', revealTarget);
  revealTarget();
</script>
'@

$manifest = Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
function WritePage($file, $tab, $index, $content) {
    $codeCurrent = if ($tab -eq 'Code') { " aria-current='page'" }
    $assetsCurrent = if ($tab -eq 'Assets') { " aria-current='page'" }
    $version = Encode $manifest.version
    @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$tab — Quiet Static Library</title>
$style
</head>
<body>
<header>
  <div class="wrap">
    <p>Quiet Static Library <small>$version</small></p>
    <nav aria-label="Documentation tabs"><ul>
      <li><a href="index.html"$codeCurrent>Code</a></li>
      <li><a href="assets.html"$assetsCurrent>Assets</a></li>
    </ul></nav>
  </div>
</header>
<div class="wrap layout">
  <aside aria-label="Reference index">
    <label for="search">Search $($tab.ToLowerInvariant())</label>
    <input type="search" id="search">
    <div id="result" role="status"></div>
    <h2>On this page</h2>
    <ul>$($index -join [char]10)</ul>
  </aside>
  <main>
$($content -join [char]10)
  </main>
</div>
$client
</body>
</html>
"@ | Set-Content -LiteralPath (Join-Path $PSScriptRoot $file) -Encoding utf8
}

WritePage 'index.html' 'Code' $codeIndex $code
WritePage 'assets.html' 'Assets' $assetIndex $assets
