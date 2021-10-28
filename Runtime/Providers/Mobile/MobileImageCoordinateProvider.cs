using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

public class MobileImageCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    public string AnchorName;
    public GameObject TrackedImagePrefab;

#if AR_FOUNDATION
    private List<ARTrackedImage> _arTrackedImages = new List<ARTrackedImage>();
    private ARTrackedImageManager ARTrackedImageManager
    {
        get
        {
            if (_arTrackedImageManager == null)
            {
                _arTrackedImageManager = FindObjectOfType<ARTrackedImageManager>();
            }

            return _arTrackedImageManager;
        }
    }

    private ARTrackedImageManager _arTrackedImageManager;
#endif

    private List<GenericCoordinateReference> _genericCoordinateReference = new List<GenericCoordinateReference>();

    TaskCompletionSource<List<GenericCoordinateReference>> completionSource = new TaskCompletionSource<List<GenericCoordinateReference>>();

    public void DisableGenericCoordinates()
    {
#if AR_FOUNDATION
        ARTrackedImageManager.trackedImagesChanged -= ARTrackedImageManager_trackedImagesChanged;
#endif
    }

    public void InitializeGenericCoordinates()
    {
#if AR_FOUNDATION
        ARTrackedImageManager.trackedImagesChanged += ARTrackedImageManager_trackedImagesChanged;
#endif

    }
#if AR_FOUNDATION

    private void ARTrackedImageManager_trackedImagesChanged(ARTrackedImagesChangedEventArgs obj)
    {

        _arTrackedImages.Clear();

        for (int i = 0; i < obj.added.Count; i++)
        {
            _arTrackedImages.Add(obj.added[i]);
        }

        for (int i = 0; i < obj.updated.Count; i++)
        {
            if (!_arTrackedImages.Contains(obj.updated[i]))
            {
                _arTrackedImages.Add(obj.updated[i]);
            }
        }

        _genericCoordinateReference.Clear();

        for (int i = 0; i < _arTrackedImages.Count; i++)
        {
            var image = _arTrackedImages[i];

            if (image.trackingState == TrackingState.Tracking)
            {
                var genericCoordinate = new GenericCoordinateReference()
                {
                    CoordinateId = AnchorName,
                    Position = image.transform.position,
                    Rotation = image.transform.rotation
                };
                if (TrackedImagePrefab)
                {
                    TrackedImagePrefab.gameObject.SetActive(true);
                    TrackedImagePrefab.transform.position = image.transform.position;
                    TrackedImagePrefab.transform.rotation = image.transform.rotation;
                }
                _genericCoordinateReference.Add(genericCoordinate);
            }

        }

        completionSource.TrySetResult(_genericCoordinateReference);

    }
#endif

    public async Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {
#if AR_FOUNDATION
        completionSource.TrySetResult(new List<GenericCoordinateReference>());
        completionSource = new TaskCompletionSource<List<GenericCoordinateReference>>();

        if (await Task.WhenAny(completionSource.Task, Task.Delay(100000)) != completionSource.Task)
        {
            Debug.LogError("no image targets found, count: " + _arTrackedImages.Count);
            return new List<GenericCoordinateReference>();
        }

        return completionSource.Task.Result;
#endif
        return new List<GenericCoordinateReference>();
    }


}
