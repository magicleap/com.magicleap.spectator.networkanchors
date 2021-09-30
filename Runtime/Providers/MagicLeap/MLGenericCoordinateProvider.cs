using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

[System.Serializable]
public class ImageTargetInfo
{
    public string Name;
    public Texture2D Image;
    public float LongerDimension;
}

//This contains the four possible statuses we can encounter while trying to use the tracker.
public enum ImageTrackingStatus
{
    Inactive,
    PrivilegeDenied,
    ImageTrackingActive,
    CameraUnavailable
}

/// <summary>
/// Transforms Magic Leap's PCFs into generic Coordinates that can be used for co-location experiences.
/// </summary>
public class MLGenericCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    //Image Tracking

    // The image target built from the ImageTargetInfo object
    private MLImageTracker.Target _imageTarget;

    //The inspector field where we assign our target images
    public ImageTargetInfo TargetInfo;

    // The main event and statuses for Image Tracking functionality
    //public delegate void TrackingStatusChanged(ImageTrackingStatus status);
    //public static TrackingStatusChanged OnImageTrackingStatusChanged;
    //public ImageTrackingStatus CurrentStatus;

    private MLImageTracker.Target.Result _imageTargetResult;

    //These allow us to see the position and rotation of the detected image from the inspector
    public Vector3 ImagePos = Vector3.zero;
    public Quaternion ImageRot = Quaternion.identity;

    private bool _isImageTrackingInitialized = false;

    //PCFs

    //TODO: Handle loss of tracking
    private bool _isLocalized;

    private Coroutine _getGenericCoordinatesEnumerator;

    private TaskCompletionSource<List<GenericCoordinateReference>> _coordinateReferencesCompletionSource;

    private const int RequestTimeoutMs = 2000000;

    public async Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {

        CancelTasks();

        _coordinateReferencesCompletionSource = new TaskCompletionSource<List<GenericCoordinateReference>>();
        _getGenericCoordinatesEnumerator = StartCoroutine(DoGetGenericCoordinates());

        if (await Task.WhenAny(_coordinateReferencesCompletionSource.Task,
            Task.Delay(RequestTimeoutMs)) != _coordinateReferencesCompletionSource.Task)
        {
            Debug.LogError("Could not get coordinates");
            return new List<GenericCoordinateReference>();
        }

        return _coordinateReferencesCompletionSource.Task.Result;
    }

    private void CancelTasks()
    {
        if (_getGenericCoordinatesEnumerator != null)
        {
            StopCoroutine(_getGenericCoordinatesEnumerator);
            _getGenericCoordinatesEnumerator = null;
        }
        _coordinateReferencesCompletionSource?.TrySetResult(null);

    }

    public void InitializeGenericCoordinates()
    {
#if PLATFORM_LUMIN

        if (!MLPersistentCoordinateFrames.IsStarted)
        {
            MLResult result = MLPersistentCoordinateFrames.Start();
            if (!result.IsOk)
            {
                Debug.LogErrorFormat("Error: PCFExample failed starting MLPersistentCoordinateFrames, disabling script. Reason: {0}", result);
            }

            MLPersistentCoordinateFrames.OnLocalized += HandleOnLocalized;
        }
#endif
    }

    public void DisableGenericCoordinates()
    {
        CancelTasks();
#if PLATFORM_LUMIN
        MLPersistentCoordinateFrames.OnLocalized -= HandleOnLocalized;
        MLPersistentCoordinateFrames.Stop();
#endif
    }

    private IEnumerator DoGetGenericCoordinates()
    {
#if PLATFORM_LUMIN
        //system start up
        InitializeGenericCoordinates();

        if (string.IsNullOrEmpty(TargetInfo.Name) == false)
        {
            bool privilegesGranted = false;
            bool hasPrivilegesResult = false;

            
            MLPrivileges.RequestPrivilegesAsync(MLPrivileges.Id.CameraCapture).ContinueWith((x) =>
            {
                if (x.Result.IsOk == false && x.Result != MLResult.Code.PrivilegeGranted)
                {
                    privilegesGranted = false;
                    Debug.LogError("image capture privileges not granted. Reason: " + x.Result);
                }
                else
                {
                    privilegesGranted = true;
                }

                hasPrivilegesResult = true;
            });

            
            privilegesGranted = true;
            hasPrivilegesResult = true;

            while (hasPrivilegesResult == false)
            {
                yield return null;
            }

            if (privilegesGranted == true)
            {
                _imageTarget = MLImageTracker.AddTarget(TargetInfo.Name, TargetInfo.Image, TargetInfo.LongerDimension, HandleImageTracked, true);
            }

            if (_imageTarget == null)
            {
                Debug.LogError("Cannot add image target");
            } else
            {
                _isImageTrackingInitialized = true;
            }


        }

        

        while (!MLPersistentCoordinateFrames.IsStarted || !MLPersistentCoordinateFrames.IsLocalized)
        {
            yield return null;
        }

        //After the services starts we need to wait a frame before quarrying the results.
        yield return new WaitForEndOfFrame();
        //Find the Multi User PCFs
        MLResult result = MLPersistentCoordinateFrames.FindAllPCFs(out List<MLPersistentCoordinateFrames.PCF> allPcFs, typesMask: MLPersistentCoordinateFrames.PCF.Types.MultiUserMultiSession);
        if (result != MLResult.Code.Ok)
        {
            Debug.LogError("Could not find PCFs! Result : " + result);
            CancelTasks();
            yield break;
        }
        Debug.Log("MLPersistentCoordinateFrames.FindAllPCFs Result : " + result);

        List<GenericCoordinateReference> genericPcfReferences = allPcFs.OrderByDescending(x => x.Confidence)
            .Select(x => new GenericCoordinateReference
                { CoordinateId = x.CFUID.ToString(), Position = x.Position, Rotation = x.Rotation }).ToList();
        
        Debug.Log("Found " + genericPcfReferences.Count + " MultiUserMultiSession coordinates");

        if (_isImageTrackingInitialized)
        {
            Debug.Log("123");

            if(genericPcfReferences.Count == 0)
            {
                while (_imageTargetResult.Status != MLImageTracker.Target.TrackingStatus.Tracked)
                {
                    yield return null;
                }
            }

            Debug.Log("456" + _imageTargetResult.Position);

            genericPcfReferences.Add(new GenericCoordinateReference()
            {
                CoordinateId = TargetInfo.Name,
                Position = _imageTargetResult.Position,
                Rotation = _imageTargetResult.Rotation
            });
        }

        _coordinateReferencesCompletionSource?.TrySetResult(genericPcfReferences);
#else
        yield return new WaitForEndOfFrame();

        _coordinateReferencesCompletionSource?.TrySetResult(null);

#endif
        _getGenericCoordinatesEnumerator = null;

    }

    private void HandleOnLocalized(bool localized)
    {
        //TODO: Handle loss of tracking
        _isLocalized = localized;
    }

    private void OnDisable()
    {
        CancelTasks();
    }

    private void OnDestroy()
    {
        CancelTasks();
    }

#if PLATFORM_LUMIN
    private void HandleImageTracked(MLImageTracker.Target imageTarget,
                                    MLImageTracker.Target.Result imageTargetResult)
    {
        _imageTargetResult = imageTargetResult;
    }

#endif
}
