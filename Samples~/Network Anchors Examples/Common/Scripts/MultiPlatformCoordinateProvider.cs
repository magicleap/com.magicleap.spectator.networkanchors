using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Management;

public class MultiPlatformCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    [Header("Providers")]
#if PLATFORM_LUMIN
    public MLGenericCoordinateProvider MagicLeapProvider;
#elif PLATFORM_ANDROID || PLATFORM_IOS
    public MobileImageCoordinateProvider ARFoundationProvider;
#endif
    public StandaloneCoordinateProvider StandaloneProvider;
    private bool _forceStandalone;

    void Start()
    {
        //Check if any XR managers are running. If not force standalone.
        if (!XRGeneralSettings.Instance.Manager.isInitializationComplete)
        {
            _forceStandalone = true;
            Debug.Log("Not initialized");
        }
    }

    public Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {

#if PLATFORM_LUMIN
        if (_forceStandalone)
            return StandaloneProvider.RequestCoordinateReferences(refresh);

        return MagicLeapProvider.RequestCoordinateReferences(refresh);
#elif PLATFORM_ANDROID || PLATFORM_IOS
        return ARFoundationProvider.RequestCoordinateReferences(refresh);
#else
        return StandaloneProvider.RequestCoordinateReferences(refresh);
#endif
    }

    public void InitializeGenericCoordinates()
    {
#if PLATFORM_LUMIN
        if (_forceStandalone)
        {
            StandaloneProvider.InitializeGenericCoordinates();
            return;
        }

        MagicLeapProvider.InitializeGenericCoordinates();
#elif PLATFORM_ANDROID || PLATFORM_IOS
        ARFoundationProvider.InitializeGenericCoordinates();
#else
        StandaloneProvider.InitializeGenericCoordinates();
#endif
    }

    public void DisableGenericCoordinates()
    {
#if PLATFORM_LUMIN
        if (_forceStandalone)
        {
            StandaloneProvider.DisableGenericCoordinates();
            return;
        }

        MagicLeapProvider.DisableGenericCoordinates();
#elif PLATFORM_ANDROID || PLATFORM_IOS
        ARFoundationProvider.DisableGenericCoordinates();
#else
        StandaloneProvider.DisableGenericCoordinates();
#endif
    }
}
