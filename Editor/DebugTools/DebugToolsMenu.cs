#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using QuietStatic.Toolkit.DebugTools;

namespace QuietStatic.Toolkit.DebugTools.Editor
{
    /// <summary>
    /// Provides Unity menu shortcuts for adding debug tooling components to a scene.
    /// </summary>
    /// <remarks>
    /// Each helper is exposed both in the project Tools menu and in the GameObject creation menu.
    /// Creation is registered with Unity's Undo system, so a user can undo the entire new object
    /// in one step. <see cref="GameObjectUtility.SetParentAndAlign(GameObject, GameObject)"/> also
    /// gives context-menu creations the selected parent and the same alignment behavior as native
    /// Unity GameObject commands.
    /// </remarks>
    public static class DebugToolsMenu
    {
        private const string DashboardMenuPath = "Tools/Quiet Static/Debug Dashboard";
        private const string GameObjectDashboardMenuPath = "GameObject/Quiet Static/Debug Tools Dashboard";
        private const string GameObjectTeleportAreaMenuPath = "GameObject/Quiet Static/Debug Teleport Area";

        /// <summary>
        /// Creates a dashboard below the current Hierarchy selection from the global Tools menu.
        /// </summary>
        [MenuItem(DashboardMenuPath)]
        private static void CreateDashboardFromToolsMenu()
        {
            CreateDashboard(Selection.activeGameObject);
        }

        /// <summary>
        /// Creates a dashboard using the GameObject menu's context object as its parent.
        /// </summary>
        /// <param name="command">
        /// Unity menu context; its <see cref="MenuCommand.context"/> is the selected parent when
        /// the command was opened from the Hierarchy.
        /// </param>
        [MenuItem(GameObjectDashboardMenuPath, false, 10)]
        private static void CreateDashboardFromGameObjectMenu(MenuCommand command)
        {
            CreateDashboard(command.context as GameObject);
        }

        /// <summary>Creates, parents, registers, and selects a debug dashboard object.</summary>
        /// <param name="parent">
        /// Optional scene parent. A <see langword="null"/> value creates a root GameObject.
        /// </param>
        private static void CreateDashboard(GameObject parent)
        {
            GameObject dashboardObject = new("Debug Tools");
            GameObjectUtility.SetParentAndAlign(dashboardObject, parent);

            // Register after parenting so Undo removes the complete configured hierarchy entry.
            Undo.RegisterCreatedObjectUndo(dashboardObject, "Create Debug Tools");
            dashboardObject.AddComponent<DebugDashboard>();

            // Match Unity's built-in creation commands by leaving the new object selected.
            Selection.activeGameObject = dashboardObject;
        }

        /// <summary>
        /// Creates a teleport area below the current Hierarchy selection from the Tools menu.
        /// </summary>
        private static void CreateTeleportAreaFromToolsMenu()
        {
            CreateTeleportArea(Selection.activeGameObject);
        }

        /// <summary>
        /// Creates a teleport area using the GameObject menu's context object as its parent.
        /// </summary>
        /// <param name="command">
        /// Unity menu context; its <see cref="MenuCommand.context"/> is the selected parent when
        /// invoked from the Hierarchy.
        /// </param>
        [MenuItem(GameObjectTeleportAreaMenuPath, false, 11)]
        private static void CreateTeleportAreaFromGameObjectMenu(MenuCommand command)
        {
            CreateTeleportArea(command.context as GameObject);
        }

        /// <summary>Creates, parents, registers, and selects a debug teleport-area object.</summary>
        /// <param name="parent">
        /// Optional scene parent. A <see langword="null"/> value creates a root GameObject.
        /// </param>
        private static void CreateTeleportArea(GameObject parent)
        {
            GameObject areaObject = new("Debug Teleport Area");
            GameObjectUtility.SetParentAndAlign(areaObject, parent);

            // Undoing the registered root creation removes this object and its attached component
            // together, presenting the menu action as one user-level operation.
            Undo.RegisterCreatedObjectUndo(areaObject, "Create Debug Teleport Area");
            areaObject.AddComponent<DebugTeleportArea>();
            Selection.activeGameObject = areaObject;
        }
    }
}
#endif
