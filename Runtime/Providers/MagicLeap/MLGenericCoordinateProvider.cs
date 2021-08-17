using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

/// <summary>
/// Transforms Magic Leap's PCFs into generic Coordinates that can be used for co-location experiences.
/// </summary>
public class MLGenericCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    //TODO: Handle loss of tracking
    private bool _isLocalized;

    private Coroutine _getGenericCoordinatesEnumerator;

    private TaskCompletionSource<List<GenericCoordinateReference>> _coordinateReferencesCompletionSource;

    private const int RequestTimeoutMs = 2000;

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
        if (!MLPersistentCoordinateFrames.IsStarted)
        {
            MLResult result = MLPersistentCoordinateFrames.Start();
            if (!result.IsOk)
            {
                Debug.LogErrorFormat("Error: PCFExample failed starting MLPersistentCoordinateFrames, disabling script. Reason: {0}", result);
            }

            MLPersistentCoordinateFrames.OnLocalized += HandleOnLocalized;
        }
    }

    public void DisableGenericCoordinates()
    {
        CancelTasks();
        MLPersistentCoordinateFrames.OnLocalized -= HandleOnLocalized;
        MLPersistentCoordinateFrames.Stop();
    }

    private IEnumerator DoGetGenericCoordinates()
    {
#if PLATFORM_LUMIN
        //system start up
        InitializeGenericCoordinates();

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
}
