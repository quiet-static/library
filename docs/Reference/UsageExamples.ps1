function UsageKey($type, $member, $count, $genericCount) {
    "$type/$member/$count/$genericCount"
}

function Find-UsageExamples($types, $options, $package) {
    $knownTypes = @{}
    foreach ($group in $types | Group-Object Name) {
        if (@($group.Group.QualifiedName | Sort-Object -Unique).Count -eq 1) {
            $knownTypes[$group.Name] = $group.Group[0].QualifiedName
        }
    }

    $stolen = Join-Path (Split-Path $package) 'stolen'
    $inputs = @()
    if (Test-Path -LiteralPath (Join-Path $stolen '.git')) {
        $revision = git -C $stolen rev-parse HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the Stolen revision.' }
        $dirty = @(git -C $stolen -c core.safecrlf=false diff --name-only HEAD -- 'Assets/*.cs')
        $inputs += @(git -C $stolen ls-files -- 'Assets/*.cs' |
            Where-Object { $_ -notin $dirty -and (Test-Path -LiteralPath (Join-Path $stolen $_)) } |
            Sort-Object { $_ -notlike 'Assets/Stolen/Scripts/Runtime/*' } |
            ForEach-Object {
                @{
                    Path = Join-Path $stolen $_
                    Label = "Stolen · $_"
                    Link = "https://github.com/quiet-static/stolen/blob/$revision/$_"
                }
            })
    }

    foreach ($directory in 'Samples~','Tests','Runtime','Editor') {
        $root = Join-Path $package $directory
        if (!(Test-Path -LiteralPath $root)) { continue }
        $inputs += @(Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' -File | Sort-Object FullName |
            ForEach-Object {
                $path = RelativePath $_
                @{ Path = $_.FullName; Label = $path; Link = "../../../$path" }
            })
    }

    $examples = @{}
    foreach ($inputFile in $inputs) {
        $source = [IO.File]::ReadAllText($inputFile.Path)
        $parsed = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source, $options)
        $nodes = @($parsed.GetRoot().DescendantNodes())
        $receivers = @{}
        foreach ($node in $nodes) {
            $kind = $node.GetType().Name
            $symbols = if ($kind -eq 'VariableDeclarationSyntax') { $node.Variables }
                elseif ($kind -eq 'ParameterSyntax' -or $kind -eq 'PropertyDeclarationSyntax') { $node }
            foreach ($symbol in $symbols) {
                if (!$symbol) { continue }
                $typeName = ([string]$node.Type)
                if ($typeName -eq 'var') {
                    $initializer = ([string]$symbol.Initializer.Value)
                    $typeName = switch -Regex ($initializer) {
                        '^(?:new\s+|(?:\w+\.)*(?:GetComponent|GetComponentInChildren|CreateInstance|AddComponent)<)(\w+)' { $Matches[1]; break }
                        '^(\w+)\.Instance\b' { $Matches[1] }
                    }
                }
                $name = $symbol.Identifier.ValueText
                if ($typeName -match '^(?:\w+\.)*(\w+)(?:<[^>]+>)?$') { $typeName = $Matches[1] }
                $target = if ($typeName -and $knownTypes.ContainsKey($typeName)) { $knownTypes[$typeName] } else { '' }
                if ($receivers.ContainsKey($name) -and $receivers[$name] -ne $target) { $target = '' }
                $receivers[$name] = $target
            }
            $symbols = $null
        }
        foreach ($node in $nodes) {
            $kind = $node.GetType().Name
            $name = $null
            $target = $null
            $genericCount = 0
            if ($kind -eq 'ObjectCreationExpressionSyntax' -and $node.ArgumentList) {
                $typeName = $node.Type.ToString()
                if ($typeName -match '^(?:\w+\.)*(\w+)(?:<[^>]+>)?$') { $typeName = $Matches[1] }
                $target = $knownTypes[$typeName]
                $name = '.ctor'
            } elseif ($kind -eq 'ImplicitObjectCreationExpressionSyntax' -and
                      $node.Parent.GetType().Name -eq 'EqualsValueClauseSyntax' -and
                      $node.Parent.Parent.GetType().Name -eq 'VariableDeclaratorSyntax') {
                $target = $receivers[$node.Parent.Parent.Identifier.ValueText]
                $name = '.ctor'
            } elseif ($kind -eq 'InvocationExpressionSyntax') {
                $expression = $node.Expression
                if ($expression.GetType().Name -eq 'MemberAccessExpressionSyntax') {
                    $receiver = $expression.Expression.ToString() -replace '^this\.', ''
                } elseif ($expression.GetType().Name -eq 'MemberBindingExpressionSyntax' -and
                           $node.Parent.GetType().Name -eq 'ConditionalAccessExpressionSyntax') {
                    $receiver = $node.Parent.Expression.ToString() -replace '^this\.', ''
                } else { continue }
                $name = $expression.Name.Identifier.ValueText
                $genericCount = $expression.Name.TypeArgumentList.Arguments.Count
                if ($receiver -match '^(\w+)\.Instance$') { $receiver = $Matches[1] }
                $target = if ($receivers.ContainsKey($receiver)) { $receivers[$receiver] } else { $knownTypes[$receiver] }
            }
            if (!$target -or !$name) { continue }
            $context = if ($node.Parent.GetType().Name -eq 'ConditionalAccessExpressionSyntax') { $node.Parent } else { $node }
            $statement = $context.Ancestors() | Where-Object {
                $_.GetType().Name -in 'ExpressionStatementSyntax','LocalDeclarationStatementSyntax','ReturnStatementSyntax'
            } | Select-Object -First 1
            if ($statement -and $statement.Span.Length -le 270) { $context = $statement }
            $span = $parsed.GetLineSpan($context.Span)
            $line = $span.StartLinePosition.Line + 1
            $lastLine = $span.EndLinePosition.Line + 1
            $excerpt = $context.ToString()
            if ($excerpt.Length -gt 420) { continue }
            $indent = [Math]::Min($span.StartLinePosition.Character, 80)
            if ($indent -gt 0) { $excerpt = $excerpt -replace "(?m)^ {1,$indent}", '' }
            $key = UsageKey $target $name $node.ArgumentList.Arguments.Count $genericCount
            if (!$examples.ContainsKey($key)) {
                $examples[$key] = @{
                    Code = $excerpt
                    Line = $line
                    End = $lastLine
                    Label = $inputFile.Label
                    Link = $inputFile.Link
                }
            }
        }
    }
    $examples
}

function Format-UsageExample($example) {
    $location = if ($example.End -gt $example.Line) { "lines $($example.Line)–$($example.End)" } else { "line $($example.Line)" }
    $link = $example.Link
    if ($link.StartsWith('https://')) {
        $link += "#L$($example.Line)"
        if ($example.End -gt $example.Line) { $link += "-L$($example.End)" }
    }
    "<figure class='example'><figcaption>Usage in <a href='$(Encode $link)'>$(Encode $example.Label), $location</a></figcaption><pre><code>$(Encode $example.Code)</code></pre></figure>"
}

function CallTemplate($type, $member) {
    $kind = $member.GetType().Name
    $arguments = foreach ($parameter in $member.ParameterList.Parameters) {
        $name = $parameter.Identifier.Text
        $modifier = @($parameter.Modifiers | ForEach-Object ValueText)
        if ($modifier -contains 'out') { "out var $name" }
        elseif ($modifier -contains 'ref') { "ref $name" }
        else { $name }
    }
    $typeName = $type.Name + ([string]$type.Node.TypeParameterList)
    $argsText = $arguments -join ', '
    if ($kind -eq 'ConstructorDeclarationSyntax') { return "var instance = new $typeName($argsText);" }

    $name = $member.Identifier.Text + ([string]$member.TypeParameterList)
    $receiver = if ($member.Modifiers | Where-Object ValueText -eq 'static') { $typeName } else { 'instance' }
    $prefix = if ($member.ReturnType.ToString() -ne 'void') { 'var result = ' } else { '' }
    "$prefix$receiver.$name($argsText);"
}
