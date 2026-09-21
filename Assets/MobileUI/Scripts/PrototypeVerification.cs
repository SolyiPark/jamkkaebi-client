using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MobilePrototype
{
    // Opt-in development-player integration checks. Never runs during normal play.
    public class PrototypeVerification : MonoBehaviour
    {
        private string output;
        private readonly List<string> results = new List<string>();
        private readonly List<string> errors = new List<string>();
        private IEnumerator Start()
        {
            string option = Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith("--verify-output="));
            if (option == null || !Debug.isDebugBuild) yield break;
            output = option.Substring("--verify-output=".Length); Directory.CreateDirectory(output);
            Application.logMessageReceived += RecordError;
            var run = Run();
            while (true)
            {
                bool more; object value = null;
                try { more = run.MoveNext(); if (more) value = run.Current; }
                catch (Exception e) { results.Add("FAIL " + e); Finish(false); yield break; }
                if (!more) break;
                yield return value;
            }
            Finish(errors.Count == 0);
        }
        private void RecordError(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        private void Check(bool condition, string description)
        { if (!condition) throw new Exception(description); results.Add("PASS " + description); }
        private void Finish(bool success)
        {
            Application.logMessageReceived -= RecordError;
            File.WriteAllLines(Path.Combine(output,"verification.txt"), new[] { success ? "ALL CHECKS PASSED" : "CHECKS FAILED" }.Concat(results).Concat(errors));
            Application.Quit(success ? 0 : 1);
        }
        private Vector2 ToScreen(TabHost host, Vector3 world)
        {
            var camera = host.ActiveRoot.sceneCamera;
            var uv = camera.WorldToViewportPoint(world);
            var rect = host.viewport.rect;
            var local = new Vector3(Mathf.Lerp(rect.xMin,rect.xMax,uv.x),Mathf.Lerp(rect.yMin,rect.yMax,uv.y),0);
            return RectTransformUtility.WorldToScreenPoint(null,host.viewport.TransformPoint(local));
        }
        private void Click(TabHost host, Vector3 world)
        {
            var data = new PointerEventData(EventSystem.current) { pointerId = -1, position = ToScreen(host,world), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(data); host.input.OnPointerUp(data);
        }
        private IEnumerator Run()
        {
            var host = FindFirstObjectByType<TabHost>();
            float deadline = Time.realtimeSinceStartup + 40;
            while (!host.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(host.IsReady,"four additive scenes loaded");
            yield return new WaitForSeconds(.3f);
            Check(host.ActiveIndex == 0 && host.profileBar.activeSelf,"home selected; profile visible");
            for (int i=0;i<4;i++)
            {
                Check(host.GetRoot(i).sceneCamera.cullingMask == 1 << (8+i),"isolated camera layer " + i);
                Check(host.GetRoot(i).sceneCamera.targetTexture != null,"RenderTexture allocated " + i);
            }
            var source = FindFirstObjectByType<ProfileDataSource>();
            source.SetProfile(new ProfileData { nickname = "테스트 사용자", consecutiveDays = 24 });
            Check(host.profileBar.GetComponent<ProfileBar>().nickname.text == "테스트 사용자","profile data event updates visible UI");
            source.SetProfile(new ProfileData());
            var homeCounter = host.GetRoot(0).GetComponentInChildren<DemoCounter>();
            Click(host,homeCounter.button.transform.position);
            Check(homeCounter.count == 1,"mirrored uGUI button receives pointer click");
            var drag = host.GetRoot(0).GetComponentInChildren<DemoDraggable>();
            var before = drag.transform.position;
            var p = new PointerEventData(EventSystem.current) { pointerId = -1, position = ToScreen(host,before), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(p); p.position += new Vector2(60,35);
            host.input.OnDrag(p); host.input.OnPointerUp(p);
            Check(Vector3.Distance(before,drag.transform.position) > .1f && drag.dragCount == 1,"mirrored 2D collider receives drag");
            var motion = host.GetRoot(0).GetComponentInChildren<DemoMotion>(); float elapsed = motion.elapsed;
            host.SelectTab(1); yield return new WaitForSeconds(.4f);
            Check(!host.profileBar.activeSelf && host.viewport.offsetMax.y == 0,"non-home viewport expands into header area");
            Check(motion.elapsed > elapsed + .2f,"hidden home continues running by default");
            var workshop = host.GetRoot(1).GetComponentInChildren<DemoCounter>();
            Click(host,workshop.button.transform.position);
            Check(workshop.count == 1 && homeCounter.count == 1,"input only reaches selected scene");
            host.SelectTab(0); yield return new WaitForSeconds(.2f);
            Check(homeCounter.count == 1 && drag.dragCount == 1,"tab return preserves counter and dragged object");
            host.catalog.tabs[0].backgroundPolicy = BackgroundPolicy.PauseWhileHidden;
            host.SelectTab(1); yield return null; elapsed = motion.elapsed;
            yield return new WaitForSeconds(.35f);
            Check(Mathf.Abs(motion.elapsed - elapsed) < .001f,"pause policy freezes hidden scene updates");
            host.SelectTab(0); yield return new WaitForSeconds(.2f);
            Check(motion.elapsed > elapsed && homeCounter.count == 1,"paused scene resumes with state retained");
            host.catalog.tabs[0].backgroundPolicy = BackgroundPolicy.RestartOnReturn;
            host.SelectTab(1); host.SelectTab(0);
            deadline = Time.realtimeSinceStartup + 20;
            while(host.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!host.IsBusy && host.GetRoot(0).GetComponentInChildren<DemoCounter>().count == 0,"restart-on-return reloads scene to initial state");
            host.catalog.tabs[0].backgroundPolicy = BackgroundPolicy.ContinueRunning;
            yield return new WaitForSeconds(.2f);
            homeCounter = host.GetRoot(0).GetComponentInChildren<DemoCounter>(); Click(host,homeCounter.button.transform.position);
            host.RestartCurrent(); deadline = Time.realtimeSinceStartup + 20;
            while(host.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!host.IsBusy && host.GetRoot(0).GetComponentInChildren<DemoCounter>().count == 0,"developer restart command resets current scene");
            for(int i=0;i<4;i++)
            {
                host.SelectTab(i); yield return new WaitForSeconds(.2f);
                var counter = host.GetRoot(i).GetComponentInChildren<DemoCounter>(); int old = counter.count;
                Click(host,counter.button.transform.position);
                Check(counter.count == old + 1,"pointer mapping works on tab " + i);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,$"0{i+1}_{host.catalog.tabs[i].id}.png"));
                yield return null;
            }
            Screen.SetResolution(480,1040,FullScreenMode.Windowed);
            host.SelectTab(0); yield return new WaitForSeconds(.5f);
            float textureAspect = (float)host.ActiveRoot.sceneCamera.targetTexture.width / host.ActiveRoot.sceneCamera.targetTexture.height;
            Check(Mathf.Abs(textureAspect - host.viewport.rect.width/host.viewport.rect.height) < .01f,"tall-screen render target matches viewport aspect");
            homeCounter = host.GetRoot(0).GetComponentInChildren<DemoCounter>(); int oldCount = homeCounter.count;
            Click(host,homeCounter.button.transform.position);
            Check(homeCounter.count == oldCount+1,"pointer mapping survives resolution change");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,"05_TallScreen.png")); yield return null;
            Screen.SetResolution(768,1024,FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
            Check(host.viewport.rect.height > 0,"layout supports wider portrait screen");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,"06_WidePortrait.png"));
            yield return new WaitForSeconds(.3f);
        }
    }
}
