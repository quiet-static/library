# Compatibility

These components preserve serialized Unity references and source APIs created by
earlier toolkit versions. They compile in the dedicated
`QuietStatic.Compatibility.Runtime` assembly, which depends on the authoritative
`QuietStatic.Core.Runtime` assembly. Current Runtime code never depends on the
compatibility assembly.

The legacy character bridges intentionally keep the original script GUIDs and
`QuietStatic.Characters` namespace. Existing prefabs and scenes can therefore upgrade
without losing their movement, state, or animation components. New code should use the
types in `QuietStatic.Toolkit.Characters.Player`.

Compatibility families are grouped by their former responsibility:

- `LegacyCharacters` preserves the original player-component namespaces and GUIDs.

New package prefabs, scenes, tests, and Runtime code must not reference these types.
Project-specific migration code belongs in the consuming project. A bridge can be
removed in a breaking release only after known consumers have migrated both serialized
GUID references and source-code references.
