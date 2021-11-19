using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if PLATFORM_LUMIN
using UnityEngine.XR.MagicLeap;
#endif
[System.Serializable]
public class ImageTargetInfo
{
    public string Name;
    public Texture2D Image;
    [Tooltip("The size of the long edge of the image target in meters.")]
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
#if !PLATFORM_LUMIN
#pragma warning disable 414
#endif
    //Image Tracking

#if PLATFORM_LUMIN
    // The image target built from the ImageTargetInfo object
    private MLImageTracker.Target _imageTarget;
#endif

    //The inspector field where we assign our target images
    public ImageTargetInfo TargetInfo;
    private Coroutine _searchForImageCoroutine;

#if PLATFORM_LUMIN
    private MLImageTracker.Target.Result _imageTargetResult;
#endif

    [Tooltip("How long to search for an image, in seconds")]
    public float ImageTargetSearchTime = 60;

    [Tooltip("How long to wait for to localize PCFs")]
    public float PcfSearchTime = 30;

    [Tooltip("If true, image tracking will start any time a user requests an anchor. Note: This increases the time it takes to localize when no image target is present.")]
    public bool _autoSearchForImage = true;

    //These allow us to see the position and rotation of the detected image from the inspector
    private Vector3 _imagePos = Vector3.zero;
    private Quaternion _imageRot = Quaternion.identity;

    [SerializeField]
    [Tooltip("Displays over the image target.")]
    private GameObject _imageTargetVisual;
    
    private bool _isImageTrackingInitialized = false;

    private bool _imagePrefabUpdated = false;

    //Only supports one image at the moment.
    private List<GenericCoordinateReference> _genericImageCoordinates = new List<GenericCoordinateReference>();

    private List<GenericCoordinateReference> _genericPcfReferences= new List<GenericCoordinateReference>();


    private Coroutine _getGenericCoordinatesEnumerator;

    private TaskCompletionSource<List<GenericCoordinateReference>> _coordinateReferencesCompletionSource;

    private const int RequestTimeoutMs = 20000;

#if !PLATFORM_LUMIN
#pragma warning restore 414
#endif

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

    public void SearchForImage()
    {
        if (_searchForImageCoroutine == null)
        {
            _searchForImageCoroutine = StartCoroutine(DoSearchForImage());
        }
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

        List<GenericCoordinateReference> genericCoordinateReferences = new List<GenericCoordinateReference>();

        yield return DoSearchForPCFs();

        if (_autoSearchForImage)
            yield return DoSearchForImage();

        genericCoordinateReferences.AddRange(_genericPcfReferences);
        genericCoordinateReferences.AddRange(_genericImageCoordinates);

        Debug.Log("Returning "+ genericCoordinateReferences.Count + " genericPcfReferences.");

        _coordinateReferencesCompletionSource?.TrySetResult(genericCoordinateReferences);
#else
        yield return new WaitForEndOfFrame();

        _coordinateReferencesCompletionSource?.TrySetResult(null);

#endif
        _getGenericCoordinatesEnumerator = null;

    }

    private IEnumerator DoSearchForPCFs()
    {
#if PLATFORM_LUMIN
        Debug.Log("Initializing PCFs");

        float pcfRequestTime = Time.time;
        while (Time.time - pcfRequestTime < PcfSearchTime && (!MLPersistentCoordinateFrames.IsStarted || !MLPersistentCoordinateFrames.IsLocalized))
        {
            yield return null;
        }

        //After the services starts we need to wait a frame before quarrying the results.
        yield return new WaitForEndOfFrame();

        _genericPcfReferences.Clear();

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

            _genericPcfReferences = allPcFs.OrderByDescending(x => x.Confidence)
                .Select(x => new GenericCoordinateReference
                    { CoordinateId = x.CFUID.ToString(), Position = x.Position, Rotation = x.Rotation }).ToList();

            Debug.Log("Found " + _genericPcfReferences.Count + " MultiUserMultiSession coordinates");
        }

#else
        yield return null;
#endif

    }


    private IEnumerator DoSearchForImage()
    {
#if PLATFORM_LUMIN
        Debug.Log("Initializing Image Scan");

#pragma warning disable 618
        MLImageTracker.Start();
#pragma warning restore 618
        yield return new WaitForEndOfFrame();
        MLImageTracker.Enable();

        if (string.IsNullOrEmpty(TargetInfo.Name) == false)
        {
            yield return new WaitForEndOfFrame();

            bool privilegesGranted = false;
            bool hasPrivilegesResult = false;


            MLPrivileges.RequestPrivilegesAsync(MLPrivileges.Id.CameraCapture).ContinueWith((x) =>
            {
                if (x.Result.IsOk == false && x.Result != MLResult.Code.PrivilegeGranted)
                {
                    privilegesGranted = false;
                    Debug.LogError("Image capture privileges not granted. Reason: " + x.Result);
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

        }

        yield return new WaitForEndOfFrame();

        if (_isImageTrackingInitialized)
        {
            Debug.Log("Searching for image target");
            float imageRequestedTime = Time.time;
            while (Time.time - imageRequestedTime < ImageTargetSearchTime &&
                   (_imageTargetResult.Status != MLImageTracker.Target.TrackingStatus.Tracked
                    || _imagePos.x < .01f && _imagePos.x > -0.01f))
            {
                yield return null;
            }

            if (_imageTargetResult.Status == MLImageTracker.Target.TrackingStatus.Tracked)
            {
                //We only support one image target so remove any existing ones.
                _genericImageCoordinates.Clear();

                //Wait for tracker to update position before removing target and associated update function.
                while (!_imagePrefabUpdated)
                {
                    yield return null;
                }

                Debug.Log("Image target found, adding as generic coordinate reference.");
                var imageCoordinate = new GenericCoordinateReference()
                {
                    CoordinateId = TargetInfo.Name,
                    Position = _imagePos,
                    Rotation = _imageRot
                };

                _genericImageCoordinates.Add(imageCoordinate);
            }
        }

        MLImageTracker.Disable();
        MLImageTracker.RemoveTarget(TargetInfo.Name);
        yield return new WaitForEndOfFrame();
#pragma warning disable 618
        MLImageTracker.Stop();
#pragma warning restore 618

        _searchForImageCoroutine = null;
        _isImageTrackingInitialized = false;
        _imagePrefabUpdated = false;

#else
        yield return null;
#endif
    }

    private void HandleOnLocalized(bool localized)
    {
        //TODO: Handle loss of tracking
    }

    private void OnDisable()
    {
        CancelTasks();
    }

    private void OnDestroy()
    {
        CancelTasks();
    }

    public GenericCoordinateReference GetImageCoordinateReference()
    {
        if (_genericImageCoordinates.Count > 1)
        {
            return _genericImageCoordinates[1];
        }

        return null;
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
                _imagePrefabUpdated = true;
            }
          
        }
    }

#endif
}
