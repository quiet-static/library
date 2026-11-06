# Flags and relationship values

Flags have globally unique string IDs and an explicit value kind: Boolean, Integer,
Float, or String. `FlagDatabase` owns authored definitions and defaults; one persistent
`FlagManager` owns session state. Definitions are never mutated by runtime setters.
Existing assets default to Boolean and existing SetFlag/ClearFlag/HasFlag behavior is
preserved. Numeric/string values have no implicit Boolean truthiness.

## Multiple catalogs

Create project-owned databases in the Content Workspace Flags tab. For example:

```text
Data/Databases/
  SharedFlags.asset          story.started (Boolean)
  AlexFlags.asset            npc.alex.trust (Integer), npc.alex.confided (Boolean)
  JamieFlags.asset           npc.jamie.affection (Float), npc.jamie.nickname (String)
```

Assign the legacy **Database** field and any **Additional Flag Databases** on the one
persistent FlagManager. Either field can supply catalogs; the legacy field may be empty.
Only explicitly assigned catalogs are available at runtime. Editor flag pickers search
all imported catalogs and show catalog names, types, and descriptions. A picker result
therefore still needs its catalog assigned to the runtime manager.

IDs must be unique across catalogs, including catalogs belonging to different NPCs.
Repeating the same database reference is harmless; repeating an ID in different
entries fails manager initialization rather than choosing a definition arbitrarily.
Validation detects duplicate IDs and invalid types/defaults. Safe ID Rename checks
other catalogs for collisions both during preview and before applying the rename.
`Database` remains the legacy single-asset API; `Databases` exposes the complete set.

Without assigned catalogs the manager retains its legacy arbitrary-Boolean-ID mode.
Typed values always require a definition. With catalogs assigned, unknown IDs cannot
be set. Existing Boolean flags start false; Starting Flags supplies their initial true
values. Integer, Float, and String definitions have authored defaults. Float defaults
and values must be finite. Null strings normalize to empty strings.

## Reading and changing values

```csharp
flags.SetFlag("npc.alex.confided");
flags.SetInteger("npc.alex.trust", 10);
flags.AddInteger("npc.alex.trust", 2);
flags.SetFloat("npc.jamie.affection", 1.5f);
flags.AddFloat("npc.jamie.affection", 0.25f);
flags.SetString("npc.jamie.nickname", "Jay");
if (flags.TryGetValue("npc.alex.trust", out FlagValue value))
    Debug.Log(value.Integer); // Read the payload matching value.Type.
```

SetValue and typed setters return false for unknown IDs, mismatched kinds, nonfinite
floats, or overflowing integer addition. Successful no-op assignments return true and
raise no events. Existing Boolean setters act only on Boolean IDs. `ActiveFlags`
contains true Boolean IDs, not numeric/string values. `ValueChanged` identifies single
value changes; `FlagsChanged` also fires on typed mutations so objectives and other
existing listeners reevaluate. Bulk clear/restore emits collection notifications.

Use `FlagHandler` from scene UnityEvents: configure Values To Set and call SetValues,
or configure Value Flag Id and call SetBoolean, SetInteger, SetFloat, SetString,
AddInteger, or AddFloat. Existing SetFlag/SetFlags remain Boolean operations. Dialogue
node/choice flag-to-set lists and dependency rules also remain Boolean operations.

## Requirements and preview

Any existing FlagRequirement can combine its Boolean mode/list with additional typed
Comparisons. All comparisons must pass along with the Boolean condition. Use mode None
for a comparison-only rule, such as `npc.alex.trust >= Integer 10`. Such a rule counts
as configured for automatic objective activation/completion. Supported operators are
Equal, NotEqual, Greater, GreaterOrEqual, Less, and LessOrEqual. Ordering works only
for numeric kinds. Comparisons require matching types; String equality is ordinal,
Float equality is exact, and invalid conditions fail closed. Boolean mode lists must
not contain numeric/string IDs; use a comparison instead.

Dialogue Workspace Path Preview accepts Simulated Flags and explicit Simulated Values.
A non-Boolean comparison without a simulated value fails rather than silently passing.
Simulation never changes runtime state. The debug dashboard displays values from all
runtime catalogs unless its optional database override narrows the display.

## Saving and authoring JSON

Save schema 2 stores Boolean state and typed values, including defaults. Restore is
validated before state changes and occurs before scene/story evaluation. Failed load
rollback restores both kinds of state. Version-1 saves migrate by preserving their
Boolean IDs and using catalog defaults for newly introduced typed values. ClearAllFlags
and legacy RestoreFlags reset typed overrides to their defaults. Changing an ID's kind
in a shipped catalog requires an explicit save migration.

Export JSON preserves `valueType` (Boolean/Integer/Float/String), `defaultInteger`,
`defaultFloat`, and `defaultString` in flag catalog items. Legacy catalogs without those
fields import as Boolean with zero/empty defaults. Objective and dialogue condition JSON
also preserve `comparisons` with `flagId`, `comparison`, and a serialized `expected`
FlagValue. The existing schema-1 envelopes remain readable; external narrative authoring
tools must preserve these extension fields to round-trip typed content. Back up and
compare exports when using authorers that predate typed values.

Keep stable IDs, preview imports/renames, and use project-owned prefab variants.
Do not use progression values for rapidly changing health, physics, or movement state.
