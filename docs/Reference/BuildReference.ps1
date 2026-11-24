# Run with PowerShell 7 from the package root: pwsh -File docs/Reference/BuildReference.ps1
$ErrorActionPreference = 'Stop'
$docs = Split-Path $PSScriptRoot
$package = Split-Path $docs
. (Join-Path $PSScriptRoot 'UsageExamples.ps1')
. (Join-Path $PSScriptRoot 'RenderGuide.ps1')
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


$guideFiles = @{}
foreach ($file in @($files) + @(Get-ChildItem -LiteralPath $docs, (Join-Path $package 'Samples~') -Recurse -File -Filter '*.md') + @(Get-Item (Join-Path $package 'README.md'))) {
    if ($file.Extension -eq '.md') { $guideFiles[$file.FullName] = $file }
}
$examples = Find-UsageExamples $types $options $package
$modules = [ordered]@{}
foreach ($module in $types | Group-Object Module) {
    $id = 'module-' + ($module.Name -replace ' / ', '-').ToLowerInvariant()
    $modules[$id] = @{
        Id = $id
        Name = $module.Name
        Types = $module.Group
        Page = "Reference/Modules/$id.html"
    }
}
$typePages = @{}
foreach ($module in $modules.Values) {
    foreach ($type in $module.Types) { $typePages[$type.QualifiedName] = $module.Page }
}
$moduleScenarios = @{
    'module-editor-debugtools' = '<p>During Play Mode, open <strong>Tools → Quiet Static → Debug Dashboard</strong> to inspect the current session. The menu command is defined in <a href="../../../Editor/DebugTools/DebugToolsMenu.cs">DebugToolsMenu.cs</a>, line 27.</p>'
    'module-editor-horror' = '<p>Select a <code>HorrorTensionDefinition</code> asset and enter a default state ID. The Inspector&#39;s <strong>Tension Validation</strong> section reports an ID that does not match a configured state. See <a href="../../../Editor/Horror/HorrorTensionDefinitionEditor.cs">HorrorTensionDefinitionEditor.cs</a>, lines 23–31.</p>'
    'module-editor-narrative' = '<p>Select a <code>StorySequenceDefinition</code> asset and edit a stage&#39;s next-stage ID. Check <strong>Sequence Validation</strong> in the Inspector for unresolved stage links. See <a href="../../../Editor/Narrative/StorySequenceDefinitionEditor.cs">StorySequenceDefinitionEditor.cs</a>, lines 13–31.</p>'
    'module-editor-settings' = '<p>Use the settings and pause prefabs from <strong>Packages → Quiet Static Library → Runtime → UI → Prefabs</strong> as starting points. The editor initializer queues <code>BuildAll</code> when the expected prefab setup is missing. See <a href="../../../Editor/Settings/SettingsMenuPrefabBuilder.cs">SettingsMenuPrefabBuilder.cs</a>, lines 20–41.</p>'
    'module-editor-jumpscare' = '<p>Create a variant of <code>CustomJumpscare.prefab</code> and configure its reveal visual, audio, and timing. The editor initializer builds the base prefab if it is missing. See <a href="../../../Editor/Jumpscare/JumpscarePrefabBuilder.cs">JumpscarePrefabBuilder.cs</a>, lines 15–31.</p>'
    'module-runtime-animation' = '<p>Add <code>LightFlicker</code> to a GameObject with a <code>Light</code>. Wire an interaction&#39;s UnityEvent to <code>LightFlicker.StartFlickering</code>, and a second event to <code>StopFlickering</code> to restore the original light state. See <a href="../../../Runtime/Animation/Environment/LightFlicker.cs">LightFlicker.cs</a>, lines 137–165.</p>'
    'module-runtime-spawning' = '<p>Give the persistent player a <code>SpawnTarget</code> with ID <code>Player</code>. Add a <code>SpawnPoint</code> with ID <code>Hallway</code> to the destination scene, then use those IDs in a scene transition or spawn request. See <a href="../../../Runtime/Spawning/SpawnTarget.cs">SpawnTarget.cs</a>, lines 51–59.</p>'
}

$code = [Collections.Generic.List[string]]::new()
$code.AddRange([string[]]@(
    '<h1>Code reference</h1>'
    '<p>Search Unity 6.3 runtime and editor APIs by type, namespace, or public member. Open a module to read its setup instructions, examples, and API reference.</p>'
    '<p><a href="Reference/guides.html">Browse all setup and authoring guides</a></p>'
    "<details class='guide'><summary>Package overview and installation</summary>$(RenderGuide (Get-Item (Join-Path $package 'README.md')) 'index.html')</details>"
))
$codeIndex = foreach ($module in $modules.Values) {
    $label = Encode $module.Name
    $id = $module.Id
    "<li><a href='#$id'>$label</a></li>"
    $code.Add("<section class='catalog' id='$id'><h2><a href='$($module.Page)'>$label</a></h2><ul class='type-index'>")
    foreach ($group in $module.Types | Group-Object QualifiedName | Sort-Object { $_.Group[0].Name }) {
        $type = $group.Group[0]
        $qualified = Encode $type.QualifiedName
        $members = foreach ($part in $group.Group) {
            foreach ($member in $part.Node.Members | Where-Object { (IsPublic $_) -and !(IsType $_) }) {
                if ($member.Identifier) { $member.Identifier.ValueText }
                if ($member.Declaration.Variables) { $member.Declaration.Variables.Identifier.ValueText }
            }
        }
        $indexTerms = Encode ($members -join ' ')
        $code.Add("<li data-entry data-search='$indexTerms' id='api-$qualified'><a href='$($module.Page)#api-$qualified'><code>$qualified</code></a></li>")
    }
    $code.Add('</ul></section>')
}

function WriteModulePage($module) {
    $label = Encode $module.Name
    $page = $module.Page
    $parts = [Collections.Generic.List[string]]::new()
    $parts.Add("<h1>$label</h1>")
    $parts.Add('<p><a href="../../index.html">All modules</a> · <a href="#api">Public API</a></p>')
    $moduleFolder = Join-Path $package ($module.Name -replace ' / ', '/')
    $moduleReadmes = if ($module.Name -eq 'Editor') {
        Get-ChildItem -LiteralPath $moduleFolder -Filter README.md -File
    } else {
        Get-ChildItem -LiteralPath $moduleFolder -Filter README.md -File -Recurse | Sort-Object FullName
    }
    foreach ($readme in $moduleReadmes) {
        $parts.Add((RenderGuide $readme $page))
    }

    $parts.Add("<section class='catalog' id='api'><h2>Public API</h2><p>Source excerpts show calls from Stolen, toolkit samples, tests, or library code. In call patterns, supply the named arguments and use an instance of the documented type for <code>instance</code>.</p>")
    $moduleExample = $null
    $moduleMenu = $null
    foreach ($group in $module.Types | Group-Object QualifiedName | Sort-Object { $_.Group[0].Name }) {
        $type = $group.Group[0]
        $qualified = Encode $type.QualifiedName
        $parts.Add("<details data-entry id='api-$qualified'><summary><code>$(Encode $type.Name)</code> <span class='source'>$qualified</span></summary><div class='entry'>")
        foreach ($part in $group.Group) {
            $path = Encode $part.Path
            $line = $part.Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            $parts.Add((Signature $part.Node))
            $parts.Add("<p class='source'><a href='../../../$path'>$path, line $line</a> · <a href='#api-$qualified'>Permalink</a></p>")
            $parts.AddRange([string[]]@(DocComment $part.Node))
            foreach ($member in $part.Node.Members | Where-Object { (IsPublic $_) -and !(IsType $_) }) {
                $memberStart = $member.GetLocation().GetLineSpan().StartLinePosition
                $memberLine = $memberStart.Line + 1
                $memberId = "api-$qualified-$($path -replace '[^a-zA-Z0-9]+', '-')-$memberLine-$($memberStart.Character)"
                $parts.Add("<div class='member' id='$memberId'>$(Signature $member)<p class='source'><a href='../../../$path'>Source, line $memberLine</a> · <a href='#$memberId'>Permalink</a></p>")
                $parts.AddRange([string[]]@(DocComment $member))
                if ($member.GetType().Name -in 'MethodDeclarationSyntax','ConstructorDeclarationSyntax' -and
                    $member.Identifier.ValueText -notin 'ToString','Equals','GetHashCode','OnEnable','OnDisable','OnDestroy','OnInspectorGUI') {
                    $name = if ($member.GetType().Name -eq 'ConstructorDeclarationSyntax') { '.ctor' } else { $member.Identifier.ValueText }
                    $key = UsageKey $type.QualifiedName $name $member.ParameterList.Parameters.Count $member.TypeParameterList.Parameters.Count
                    $peerCount = @($group.Group.Node.Members | Where-Object {
                        $_.GetType() -eq $member.GetType() -and $_.Identifier.ValueText -eq $member.Identifier.ValueText -and
                        $_.ParameterList.Parameters.Count -eq $member.ParameterList.Parameters.Count -and
                        $_.TypeParameterList.Parameters.Count -eq $member.TypeParameterList.Parameters.Count
                    }).Count
                    $example = if ($peerCount -eq 1) { $examples[$key] }
                    $exampleHtml = if ($example) { Format-UsageExample $example } else {
                        "<figure class='example'><figcaption>Call pattern</figcaption><pre><code>$(Encode (CallTemplate $part $member))</code></pre></figure>"
                    }
                    $parts.Add($exampleHtml)
                    $priority = if ($example.Link -like 'https://*') { 3 } elseif ($example) { 2 } else { 1 }
                    if (!$moduleExample -or $priority -gt $moduleExample.Priority) {
                        $moduleExample = @{ Html = $exampleHtml; Priority = $priority; Type = $qualified }
                    }
                }
                $parts.Add('</div>')
            }
        }
        $parts.Add('</div></details>')
    }

    # Unity menu commands and assets are useful examples for modules driven by the Inspector.
    foreach ($file in $files | Where-Object Extension -eq '.cs') {
        $path = RelativePath $file
        if (!$file.FullName.StartsWith($moduleFolder + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($module.Name -eq 'Editor' -and $path.Split('/').Count -gt 2) { continue }
        $menu = Select-String -LiteralPath $file.FullName -Pattern '\[(?:MenuItem\(\s*|CreateAssetMenu\([^]]*?menuName\s*=\s*)"([^"]+)"' | Select-Object -First 1
        if ($menu) {
            $command = Encode $menu.Matches[0].Groups[1].Value
            $folder = if ($menu.Line -match 'CreateAssetMenu') { 'Assets → Create → ' } else { '' }
            $moduleMenu = "<p>In Unity, choose <strong>$folder$command</strong>. <a href='../../../$(Encode $path)'>$(Encode $path), line $($menu.LineNumber)</a>.</p>"
            break
        }
    }
    if ($moduleScenarios.ContainsKey($module.Id)) {
        $exampleContent = $moduleScenarios[$module.Id]
    } elseif ($moduleExample -and $moduleExample.Priority -gt 1) {
        $exampleContent = "<p>Example for <a href='#api-$($moduleExample.Type)'><code>$($moduleExample.Type)</code></a>.</p>$($moduleExample.Html)"
    } elseif ($moduleMenu) {
        $exampleContent = $moduleMenu
    } elseif ($moduleExample) {
        $exampleContent = "<p>Call pattern for <a href='#api-$($moduleExample.Type)'><code>$($moduleExample.Type)</code></a>.</p>$($moduleExample.Html)"
    }
    if ($exampleContent) { $parts.Insert(2, "<section id='example'><h2>Usage example</h2>$exampleContent</section>") }
    $parts.Add('</section>')
    $index = @("<li><a href='#api'>Public API</a></li>")
    if ($exampleContent) { $index = @("<li><a href='#example'>Usage example</a></li>") + $index }
    WritePage $page 'Code' $index $parts "$($module.Name) — API reference"
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
        $assets.Add("<p>$($sampleNotes[$folder])</p>")
        $readme = Join-Path $package "Samples~/$folder/README.md"
    } else {
        $readme = Join-Path $package "$area/$folder/README.md"
    }
    if ($guideFiles.ContainsKey($readme)) {
        $assets.Add("<details class='guide'><summary>$label setup guide</summary>$(RenderGuide $guideFiles[$readme] 'Reference/assets.html')</details>")
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
            $assets.Add("<img src='../../$path' alt='$name texture'>")
        } else {
            $references = [regex]::Matches([IO.File]::ReadAllText($asset.FullName), 'm_Script: \{[^\r\n]*?guid: (\w+)')
            $scripts = @($references | ForEach-Object { $scriptNames[$_.Groups[1].Value] } | Where-Object { $_ } | Sort-Object -Unique)
            if ($scripts.Count) {
                $links = foreach ($script in $scripts) {
                    $targets = @($types | Where-Object Name -eq $script | Select-Object -ExpandProperty QualifiedName -Unique)
                    $script = Encode $script
                    if ($targets.Count -eq 1) {
                        $url = OutputLink 'Reference/assets.html' (Join-Path $docs $typePages[$targets[0]])
                        "<a href='$(Encode $url)#api-$(Encode $targets[0])'><code>$script</code></a>"
                    } else { "<code>$script</code>" }
                }
                $assets.Add("<p>QS scripts $($links -join ', ')</p>")
            }
        }
        $assets.Add("<p class='source'><a href='../../$path'>$path</a> · <a href='#$anchor'>Permalink</a></p></article>")
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
  .guide { overflow-wrap: anywhere; }
  section.guide { margin-block: 1.35rem; }
  details.guide > section { padding: .75rem 1rem; }
  .guide h1 { font-size: 1.5rem; }
  .guide h2 { font-size: 1.2rem; }
  .guide table { display: block; max-width: 100%; overflow: auto; border-collapse: collapse; }
  .guide th, .guide td { border: 1px solid var(--line); padding: .4rem .6rem; text-align: left; }
  .guide th, .example pre, .guide pre { background: var(--wash); }
  .guide pre, .example pre { padding: .65rem .8rem; }
  .example { margin: .6rem 0 1rem; }
  .example figcaption { font-size: .9rem; color: var(--muted); }
  .example pre { margin: .25rem 0; }
  .guide img { max-width: 100%; width: auto; padding: 0; background: transparent; }
  .type-index { overflow-wrap: anywhere; padding-inline-start: 1.25rem; }
  [hidden] { display: none !important; }
  @media (max-width: 48rem) { .layout { grid-template-columns: minmax(0, 1fr); } aside ul { columns: 2; } }
</style>
'@
$client = @'
<script>
  const search = document.querySelector('#search');
  const sections = Array.from(document.querySelectorAll('.catalog')).map(section => ({
    section,
    link: document.querySelector('a[href="#' + section.id + '"]')?.closest('li'),
    items: Array.from(section.querySelectorAll('[data-entry]')).map(entry => ({
      entry,
      text: (entry.textContent + ' ' + (entry.dataset.search || '')).toLowerCase()
    }))
  }));
  if (!sections.length) search.closest('aside').querySelectorAll('label, input').forEach(item => item.hidden = true);
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
      if (link) link.hidden = section.hidden;
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
    for (let details = target.closest('details'); details; details = details.parentElement.closest('details')) {
      details.open = true;
    }
    target.scrollIntoView();
  }

  window.addEventListener('hashchange', revealTarget);
  revealTarget();
</script>
'@

$manifest = Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
function Write-PageMetadata([string] $file, [string] $output) {
    if (Test-Path -LiteralPath "$output.meta") { return }
    $bytes = [Text.Encoding]::UTF8.GetBytes("$($manifest.name)/docs/$file")
    $guid = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).Substring(0, 32).ToLowerInvariant()
    @"
fileFormatVersion: 2
guid: $guid
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@ | Set-Content -LiteralPath "$output.meta" -Encoding utf8
}

function WritePage($file, $tab, $index, $content, $title = $tab) {
    $codeCurrent = if ($tab -eq 'Code') { " aria-current='page'" }
    $assetsCurrent = if ($tab -eq 'Assets') { " aria-current='page'" }
    $guidesCurrent = if ($tab -eq 'Guides') { " aria-current='page'" }
    $codeLink = OutputLink $file (Join-Path $docs 'index.html')
    $assetsLink = OutputLink $file (Join-Path $docs 'Reference/assets.html')
    $guidesLink = OutputLink $file (Join-Path $docs 'Reference/guides.html')
    $searchLabel = if ($file -like 'Reference/Modules/*') { 'Search this module' } else { "Search $($tab.ToLowerInvariant())" }
    $version = Encode $manifest.version
    $output = Join-Path $docs $file
    @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$(Encode $title) — Quiet Static Library</title>
$style
</head>
<body>
<header>
  <div class="wrap">
    <p>Quiet Static Library <small>$version</small></p>
    <nav aria-label="Documentation tabs"><ul>
      <li><a href="$codeLink"$codeCurrent>Code</a></li>
      <li><a href="$assetsLink"$assetsCurrent>Assets</a></li>
      <li><a href="$guidesLink"$guidesCurrent>Guides</a></li>
    </ul></nav>
  </div>
</header>
<div class="wrap layout">
  <aside aria-label="Reference index">
    <label for="search">$searchLabel</label>
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
"@ | Set-Content -LiteralPath $output -Encoding utf8
    Write-PageMetadata $file $output
}

WritePage 'index.html' 'Code' $codeIndex $code
WritePage 'Reference/assets.html' 'Assets' $assetIndex $assets

$guides = [Collections.Generic.List[string]]::new()
$guides.Add('<h1>Guides</h1><p>Setup, authoring, and module notes for the Quiet Static Library.</p>')
$guideIndex = foreach ($file in $guideFiles.Values | Sort-Object FullName) {
    $path = RelativePath $file
    $name = if ($file.Name -eq 'README.md' -and $path -ne 'README.md') {
        Split-Path $file.DirectoryName -Leaf
    } else { $file.BaseName }
    $id = GuideId $path
    "<li><a href='#$id'>$(Encode $name) <small>$(Encode $path)</small></a></li>"
    $guides.Add((RenderGuide $file 'Reference/guides.html'))
}
WritePage 'Reference/guides.html' 'Guides' $guideIndex $guides

New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Modules') -Force | Out-Null
foreach ($module in $modules.Values) {
    WriteModulePage $module
}
