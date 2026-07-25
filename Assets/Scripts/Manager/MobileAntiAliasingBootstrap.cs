using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MobileAntiAliasingBootstrap
{
    private enum DeviceTier
    {
        Low,
        Medium,
        High
    }

    private const int LowEndSystemMemoryMb = 3000;
    private const int LowEndCpuCores = 4;
    private const int LowEndCpuFrequencyMhz = 1800;

    private const int MediumEndSystemMemoryMb = 5000;
    private const int MediumEndCpuCores = 6;

    private const int MsaaMedium = 2;
    private const int MsaaHigh = 4;

    private const string PostProcessLayerTypeName = "UnityEngine.Rendering.PostProcessing.PostProcessLayer";
    private const string PostProcessLayerAaFieldName = "antialiasingMode";
    private const int PostProcessAANoneEnumValue = 0;
    private const int PostProcessAAFastApproximateEnumValue = 1;

    private static bool initialized;
    private static bool triedResolvePostProcess;
    private static Type postProcessLayerType;
    private static FieldInfo postProcessLayerAaField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        ApplyCurrentPolicy();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCurrentPolicy();
    }

    private static void ApplyCurrentPolicy()
    {
        DeviceTier tier = ClassifyDeviceTier();
        bool useFsaa = tier == DeviceTier.Low;

        int msaaSamples = 0;
        if (tier == DeviceTier.Medium)
        {
            msaaSamples = MsaaMedium;
        }
        else if (tier == DeviceTier.High)
        {
            msaaSamples = MsaaHigh;
        }

        QualitySettings.antiAliasing = msaaSamples;

        Camera[] cameras = Camera.allCameras;
        bool allowMsaa = msaaSamples > 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            camera.allowMSAA = allowMsaa;
            ConfigureFsaaOnCamera(camera, useFsaa);
        }
    }

    private static DeviceTier ClassifyDeviceTier()
    {
        if (Application.isEditor)
        {
            return DeviceTier.High;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        int cpuCores = SystemInfo.processorCount;
        int cpuFrequencyMhz = SystemInfo.processorFrequency;

        bool lowByMemory = memoryMb > 0 && memoryMb <= LowEndSystemMemoryMb;
        bool lowByCpuCores = cpuCores > 0 && cpuCores <= LowEndCpuCores;
        bool lowByCpuFrequency = cpuFrequencyMhz > 0 && cpuFrequencyMhz <= LowEndCpuFrequencyMhz;
        if (lowByMemory || lowByCpuCores || lowByCpuFrequency)
        {
            return DeviceTier.Low;
        }

        bool mediumByMemory = memoryMb > 0 && memoryMb <= MediumEndSystemMemoryMb;
        bool mediumByCpuCores = cpuCores > 0 && cpuCores <= MediumEndCpuCores;
        if (mediumByMemory || mediumByCpuCores)
        {
            return DeviceTier.Medium;
        }

        return DeviceTier.High;
    }

    private static void ConfigureFsaaOnCamera(Camera camera, bool enableFsaa)
    {
        if (!TryResolvePostProcessLayerAaField())
        {
            return;
        }

        Component postProcessLayer = camera.GetComponent(postProcessLayerType);
        if (postProcessLayer == null)
        {
            return;
        }

        int enumValue = enableFsaa ? PostProcessAAFastApproximateEnumValue : PostProcessAANoneEnumValue;
        object aaMode = Enum.ToObject(postProcessLayerAaField.FieldType, enumValue);
        postProcessLayerAaField.SetValue(postProcessLayer, aaMode);
    }

    private static bool TryResolvePostProcessLayerAaField()
    {
        if (triedResolvePostProcess)
        {
            return postProcessLayerType != null && postProcessLayerAaField != null;
        }

        triedResolvePostProcess = true;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly assembly = assemblies[i];
            Type resolvedType = assembly.GetType(PostProcessLayerTypeName, false);
            if (resolvedType == null)
            {
                continue;
            }

            FieldInfo resolvedField = resolvedType.GetField(PostProcessLayerAaFieldName, BindingFlags.Instance | BindingFlags.Public);
            if (resolvedField == null || !resolvedField.FieldType.IsEnum)
            {
                continue;
            }

            postProcessLayerType = resolvedType;
            postProcessLayerAaField = resolvedField;
            return true;
        }

        return false;
    }
}