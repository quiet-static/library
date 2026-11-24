using System.Collections.Generic;
using QuietStatic.Toolkit.Objectives;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ContentIdReferenceFixture : ScriptableObject
    {
        [ObjectiveId] public List<string> tracked = new();
        public string objectiveIdNote;
    }
}
