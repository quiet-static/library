namespace QuietStatic.Toolkit.Editor.Samples
{
    /// <summary>
    /// Compatibility entry point for the former generated documentation scenes.
    /// The maintained executable sample is serialized in the package and imported
    /// explicitly; opening a project never writes into its package cache.
    /// </summary>
    public static class DocumentationSampleSceneBuilder
    {
        /// <summary>Imports, configures, and opens the maintained executable sample.</summary>
        public static void BuildAll()
        {
            ExecutableSampleSetup.ImportAndOpen();
        }
    }
}
