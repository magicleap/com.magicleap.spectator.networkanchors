using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MobileImageCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    public ARTrackedImageManager ARTrackedImageManager;
    public string AnchorName;

    private List<ARTrackedImage> _arTrackedImages = new List<ARTrackedImage>();
    private List<GenericCoordinateReference> _genericCoordinateReference = new List<GenericCoordinateReference>();

    TaskCompletionSource<List<GenericCoordinateReference>> completionSource = new TaskCompletionSource<List<GenericCoordinateReference>>();

    public void DisableGenericCoordinates()
    {
        ARTrackedImageManager.trackedImagesChanged -= ARTrackedImageManager_trackedImagesChanged;

    }

    public void InitializeGenericCoordinates()
    {
        ARTrackedImageManager.trackedImagesChanged += ARTrackedImageManager_trackedImagesChanged;
    }

    private void ARTrackedImageManager_trackedImagesChanged(ARTrackedImagesChangedEventArgs obj)
    {
        //for (int i = 0; i < obj.removed.Count; i++)
        //{
        //    if (_arTrackedImages.Contains(obj.removed[i]))
        //    {
        //        _arTrackedImages.Remove(obj.removed[i]);
        //    }
        //}
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

                _genericCoordinateReference.Add(genericCoordinate);
            }

        }

        completionSource.TrySetResult(_genericCoordinateReference);

    }

    public async Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {
        completionSource.TrySetResult(new List<GenericCoordinateReference>());
        completionSource = new TaskCompletionSource<List<GenericCoordinateReference>>();

        if (await Task.WhenAny(completionSource.Task, Task.Delay(10000)) != completionSource.Task)
        {
            Debug.LogError("no image targets found, count: " + _arTrackedImages.Count);
            return new List<GenericCoordinateReference>();
        }

        return completionSource.Task.Result;
    }


}
