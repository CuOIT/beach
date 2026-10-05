using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WaveLab.EditorTools
{
    public static class WaveLabBuild
    {
        public static void BuildAndroid()
        {
            string output="Build/WaveLab.apk";
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-waveLabOutput")output=args[i+1];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            if(!Resources.Load<WaveLabImmersionCatalog>(WaveLabImmersionCatalog.ResourcePath))
                throw new InvalidOperationException("Missing WaveLab immersion catalog.");
            // The APK launches the demo, independently of the project's Chess startup scene.
            EditorUserBuildSettings.buildAppBundle=false;
            PlayerSettings.productName="Shoreline Wave Lab";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.cuoit.wavelab");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait=true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=true;
            PlayerSettings.allowedAutorotateToLandscapeRight=true;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{WaveLabSceneBuilder.ScenePath},locationPathName=output,
                target=BuildTarget.Android,options=BuildOptions.None});
            Debug.Log("[WaveLabBuild] "+report.summary.result+" | "+report.summary.totalSize+" bytes | "+output);
            if(report.summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException("WaveLab APK build failed: "+report.summary.totalErrors+" errors.");
        }
    }
}
