using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WaveLab.Tests
{
    public sealed class WaveLabRegressionTests
    {
        GameObject root,cameraObject;
        WaveLabController controller;
        [SetUp] public void SetUp()
        {
            root=new GameObject("WaveLab regression");controller=root.AddComponent<WaveLabController>();
            controller.showInterface=false;controller.paused=true;
            cameraObject=new GameObject("Test camera");var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=8;camera.transform.position=new Vector3(0,0,-20);
            controller.Configure(Shader.Find("WaveLab/Surf Layers"),Shader.Find("WaveLab/Beach Objects"),camera);
            controller.Rebuild();
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);
        }
        [Test] public void Rebuild_ConfiguresAllImmersionAndBakedFoam()
        {
            Assert.AreEqual(93,controller.ImmersionObjects.Count);
            foreach(var item in controller.ImmersionObjects)
            {
                Assert.IsNotNull(item.SilhouetteAtlas);Assert.IsNotNull(item.ExposedRenderer);
                Assert.IsNotNull(item.ContactFoam);Assert.Greater(item.SilhouetteDistanceRange,0);
            }
            controller.Rebuild();Assert.AreEqual(93,controller.ImmersionObjects.Count);
        }
        Vector2[] Positions()
        {
            var result=new Vector2[controller.BodyCount];
            for(int i=0;i<result.Length;i++)result[i]=controller.Bodies[i].position;
            return result;
        }
        void AssertPositions(Vector2[] expected)
        {
            for(int i=0;i<expected.Length;i++)Assert.Less(Vector2.Distance(expected[i],controller.Bodies[i].position),.00001f,"body "+i);
        }
        [Test] public void Seek_CheckpointsMatchFreshReplayInBothDirections()
        {
            controller.Seek(12.345f);var expected=Positions();
            controller.Seek(4.2f);controller.Seek(12.345f);AssertPositions(expected);
            controller.ResetSimulation();controller.Seek(12.345f);AssertPositions(expected);
        }
        [Test] public void Seek_TuningChangesInvalidateTheOldTrajectory()
        {
            controller.Seek(9);controller.currentStrength=.3f;controller.advanceTime=2;
            controller.Seek(7.4f);var expected=Positions();
            controller.ResetSimulation();controller.Seek(7.4f);AssertPositions(expected);
        }
        [Test] public void QueuedSeek_CoalescesRequestsAndMatchesSynchronousReplay()
        {
            controller.Seek(8.2f);var expected=Positions();controller.ResetSimulation();
            var advance=typeof(WaveLabController).GetMethod("AdvanceSeek",BindingFlags.Instance|BindingFlags.NonPublic);
            controller.RequestSeek(15);advance.Invoke(controller,new object[]{8,0d});
            controller.RequestSeek(2);controller.RequestSeek(8.2f);
            for(int i=0;i<200&&controller.IsSeeking;i++)advance.Invoke(controller,new object[]{8,0d});
            Assert.IsFalse(controller.IsSeeking);AssertPositions(expected);
        }
        [Test] public void TouchDrag_WorksWithoutAMouseAndRetainsTheOwningFinger()
        {
            var touchscreen=InputSystem.AddDevice<Touchscreen>();
            // Quarantine physical pointer state and restore it after the test.
            var mouse=Mouse.current;bool mouseEnabled=mouse!=null&&mouse.enabled;
            if(mouseEnabled)InputSystem.DisableDevice(mouse);
            var handle=typeof(WaveLabController).GetMethod("HandlePointer",BindingFlags.Instance|BindingFlags.NonPublic);
            var body=controller.Bodies[0];
            Vector2 start=controller.SceneCamera.WorldToScreenPoint(body.transform.position);
            Vector2 end=start+new Vector2(35,20);
            try
            {
                InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
                InputSystem.Update();handle.Invoke(controller,null);
                InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=end});
                InputSystem.Update();handle.Invoke(controller,null);
                Vector2 expected=controller.SceneCamera.ScreenToWorldPoint(new Vector3(end.x,end.y,20));
                Assert.Less(Vector2.Distance(expected,body.position),.0001f);
                InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
                InputSystem.Update();handle.Invoke(controller,null);
                Assert.Less(Vector2.Distance(expected,body.position),.0001f,"A second finger must not take over the drag.");
                InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=end});
                InputSystem.Update();handle.Invoke(controller,null);
                Assert.IsNull(typeof(WaveLabController).GetField("dragged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller));
            }
            finally{InputSystem.RemoveDevice(touchscreen);if(mouseEnabled)InputSystem.EnableDevice(mouse);}
        }
        [Test] public void TouchDrag_DoesNotPickThroughUI()
        {
            var touchscreen=InputSystem.AddDevice<Touchscreen>();
            var events=new GameObject("UI test events",typeof(UnityEngine.EventSystems.EventSystem));
            var overlay=new GameObject("UI test overlay",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
            overlay.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var cover=new GameObject("UI cover",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var rect=cover.GetComponent<RectTransform>();rect.SetParent(overlay.transform,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            try
            {
                Canvas.ForceUpdateCanvases();
                Vector2 position=controller.SceneCamera.WorldToScreenPoint(controller.Bodies[0].transform.position);
                InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=position});
                InputSystem.Update();
                typeof(WaveLabController).GetMethod("HandlePointer",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
                Assert.IsNull(typeof(WaveLabController).GetField("dragged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller));
            }
            finally{InputSystem.RemoveDevice(touchscreen);Object.DestroyImmediate(overlay);Object.DestroyImmediate(events);}
        }
    }
}
