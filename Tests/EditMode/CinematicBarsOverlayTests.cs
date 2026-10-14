using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class CinematicBarsOverlayTests
    {
        [Test]
        public void Prefab_HasFullWidthBlackBarsAndDoesNotInterceptInput()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.quietstatic.core/Runtime/UI/Prefabs/CinematicBarsOverlay.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                var overlay = instance.GetComponent<CinematicBarsOverlay>();
                Assert.That(overlay, Is.Not.Null);
                Assert.That(instance.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                overlay.ShowInstant();
                var top = (RectTransform)instance.transform.Find("Top Bar");
                var bottom = (RectTransform)instance.transform.Find("Bottom Bar");
                Assert.That(top.anchorMin, Is.EqualTo(new Vector2(0f, 0.88f)));
                Assert.That(top.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(bottom.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(bottom.anchorMax, Is.EqualTo(new Vector2(1f, 0.12f)));
                foreach (var bar in new[] { top, bottom })
                {
                    var image = new SerializedObject(bar.GetComponent("Image"));
                    Assert.That(image.FindProperty("m_Color").colorValue, Is.EqualTo(Color.black));
                    Assert.That(image.FindProperty("m_RaycastTarget").boolValue, Is.False);
                    Assert.That(bar.offsetMin, Is.EqualTo(Vector2.zero));
                    Assert.That(bar.offsetMax, Is.EqualTo(Vector2.zero));
                }
                overlay.HideInstant();
                Assert.That(top.gameObject.activeSelf, Is.False);
                Assert.That(bottom.gameObject.activeSelf, Is.False);
                overlay.ShowInstant();
                Assert.That(top.gameObject.activeSelf, Is.True);
                Assert.That(bottom.gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
