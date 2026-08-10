# Narrative sequences

`StorySequenceDefinition` describes high-level story stages without owning dialogue,
scene loading, objectives, or flags. Each stage composes those existing systems through
stable IDs and reusable definitions.

Add a persistent `StorySequenceRunner`, assign a definition and optional `SceneFlowMap`,
and either enable **Start On Start** or call `StartSequence()` after bootstrap setup.
Configured flag requirements can advance stages automatically; `CompleteCurrentStage()`
supports UnityEvents and explicitly driven beats. Stage entry can activate an objective,
set flags, and request a scene-map connection. The runner implements `ISaveParticipant`.

Use one stable sequence ID per simultaneously loaded runner. The custom Inspector reports
missing IDs, duplicate IDs, invalid starting stages, and broken next-stage links.
