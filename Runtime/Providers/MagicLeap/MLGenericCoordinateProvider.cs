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
    private bool _isLookingForImage = false;
    private bool _isRequestingImageTarget = false;

#if PLATFORM_LUMIN
    // The image target built from the ImageTargetInfo object
    private MLImageTracker.Target _imageTarget;
#endif

    //The inspector field where we assign our target images
    public ImageTargetInfo TargetInfo;

#if PLATFORM_LUMIN
    private MLImageTracker.Target.Result _imageTargetResult;
#endif

    [Tooltip("How long to search for an image, in seconds")]
    public float ImageTargetSearchTime = 60;

    [Tooltip("How long to wait for to localize PCFs")]
    public float PcfSearchTime = 30;

    //These allow us to see the position and rotation of the detected image from the inspector
    private Vector3 _imagePos = Vector3.zero;
    private Quaternion _imageRot = Quaternion.identity;

    [SerializeField]
    [Tooltip("Displays over the image target.")]
    private GameObject _imageTargetVisual;
    
    private bool _isImageTrackingInitialized = false;

    private GenericCoordinateReference _imageCoordinate;

    //PCFs

    //TODO: Handle loss of tracking
    private bool _isLocalized;

    private Coroutine _getGenericCoordinatesEnumerator;

    private TaskCompletionSource<List<GenericCoordinateReference>> _coordinateReferencesCompletionSource;

    private const int RequestTimeoutMs = 20000;

    void Awake()
    {
        if (_imageTargetVisual != null)
        {
            _imageTargetVisual.SetActive(false);
        }
    }

    public async Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {

        CancelTasks();

        _coordinateReferencesCompletionSource = new TaskCompletionSource<List<GenericCoordinateReference>>();
        _getGenericCoordinatesEnumerator = StartCoroutine(DoGetGenericCoordinates());


        if (await Task.WhenAny(_coordinateReferencesCompletionSource.Task,
            Task.Delay(RequestTimeoutMs * 100)) != _coordinateReferencesCompletionSource.Task)
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
                Debug.LogErrorFormat("Error: PCFExample failed starting MLPersistentCoordinateFrames. Reason: {0}", result);
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

        Debug.Log("Initializing PCFs");

        float pcfRequestTime = Time.time;
        while (Time.time - pcfRequestTime < PcfSearchTime && (!MLPersistentCoordinateFrames.IsStarted || !MLPersistentCoordinateFrames.IsLocalized))
        {
            yield return null;
        }


        //After the services starts we need to wait a frame before quarrying the results.
        yield return new WaitForEndOfFrame();

        List<GenericCoordinateReference> genericPcfReferences = new List<GenericCoordinateReference>();

        Debug.Log("Searching for PCFs");
        //Find the Multi User PCFs
        MLResult result = MLPersistentCoordinateFrames.FindAllPCFs(out List<MLPersistentCoordinateFrames.PCF> allPcFs, typesMask: MLPersistentCoordinateFrames.PCF.Types.MultiUserMultiSession);
        if (result != MLResult.Code.Ok)
        {
            Debug.LogError("Could not find PCFs! Result : " + result);
        }
        else
        {
            Debug.Log("MLPersistentCoordinateFrames.FindAllPCFs Result : " + result);

            genericPcfReferences = allPcFs.OrderByDescending(x => x.Confidence)
                .Select(x => new GenericCoordinateReference
                    { CoordinateId = x.CFUID.ToString(), Position = x.Position, Rotation = x.Rotation }).ToList();

            Debug.Log("Found " + genericPcfReferences.Count + " MultiUserMultiSession coordinates");
        }


        if (_imageTargetResult.Status != MLImageTracker.Target.TrackingStatus.Tracked
                           || _imagePos.x < .01f && _imagePos.x > -0.01f)
        {
            _imageCoordinate = new GenericCoordinateReference()
            {
                CoordinateId = TargetInfo.Name,
                Position = _imagePos,
                Rotation = _imageRot
            };

            genericPcfReferences.Add(_imageCoordinate);
        }
        Debug.Log("Returning "+ genericPcfReferences.Count + " genericPcfReferences.");

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

        if (imageTargetResult.Status == MLImageTracker.Target.TrackingStatus.Tracked)
        {
            _imageTargetResult = imageTargetResult;
            _imagePos = imageTargetResult.Position;
            _imageRot = imageTargetResult.Rotation;
            if (_imageTargetVisual != null)
            {
                _imageTargetVisual.transform.position = imageTargetResult.Position;
                _imageTargetVisual.transform.rotation = imageTargetResult.Rotation;
                _imageTargetVisual.SetActive(true);
            }

        }

    }
#endif
    
    public IEnumerator ToggleImageScanning(bool scanImage)
    {
        _isRequestingImageTarget = scanImage;
        if (_isLookingForImage == false)
        {
            yield return DoSearchForImage();
        }

        yield return null;
    }

    private IEnumerator DoSearchForImage()
    {
        List<GenericCoordinateReference> genericPcfReferences = new List<GenericCoordinateReference>();
        _isLookingForImage = true;
        if (string.IsNullOrEmpty(TargetInfo.Name) == false)
        {
            yield return new WaitForEndOfFrame();
#if PLATFORM_LUMIN

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

            while (hasPrivilegesResult == false)
            {
                yield return null;
            }

            if (privilegesGranted == true && !_isImageTrackingInitialized)
            {
                _imageTarget = MLImageTracker.AddTarget(TargetInfo.Name, TargetInfo.Image, TargetInfo.LongerDimension, HandleImageTracked, false);
            }

            if (_imageTarget == null)
            {
                Debug.LogError("Cannot add image target");
            }
            else
            {
                Debug.Log("Image Target Added");
                _isImageTrackingInitialized = true;
            }
#endif
        }
        yield return null;

        if (_isImageTrackingInitialized)
        {
            
            float imageRequestedTime = Time.time;
            while (_isLookingForImage)
            {
                yield return null;
            }

            _imageCoordinate = new GenericCoordinateReference()
            {
                CoordinateId = TargetInfo.Name,
                Position = _imagePos,
                Rotation = _imageRot
            };

            genericPcfReferences.Add(_imageCoordinate);
        }

        _isLookingForImage = false;
        _isRequestingImageTarget = false;

        MLImageTracker.Stop();
        MLCamera.Disconnect();
    }
//#endif
}
