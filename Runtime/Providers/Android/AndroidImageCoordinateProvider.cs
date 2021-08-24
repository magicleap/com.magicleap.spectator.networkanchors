using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AndroidImageCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    public ARTrackedImageManager ARTrackedImageManager;

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

    }

    public Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {
        
        _genericCoordinateReference.Clear();

        for (int i = 0; i < _arTrackedImages.Count; i++)
        {
            var image = _arTrackedImages[i];

            if (image.trackingState == TrackingState.Tracking)
            {
                var genericCoordinate = new GenericCoordinateReference()
                {
                    CoordinateId = image.referenceImage.guid.ToString(),
                    Position = image.transform.position,
                    Rotation = image.transform.rotation
                };

                _genericCoordinateReference.Add(genericCoordinate);
            }
            
        }

        completionSource.TrySetResult(_genericCoordinateReference);
        return completionSource.Task;
    }


}
