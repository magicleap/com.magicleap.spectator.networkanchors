using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
/// <summary>
/// An interface that is used to retrieve a variety of coordinate types using a generic class.
/// </summary>
public interface IGenericCoordinateProvider
{
    /// <summary>
    /// Requests a lists of generic coordinate references / anchors.
    /// </summary>
    /// <param name="refresh">Should the provider refresh their references?</param>
    /// <returns>A list of generic coordinates</returns>
     Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh);

    /// <summary>
    /// Start the services required to query coordinates/anchors.
    /// </summary>
    void InitializeGenericCoordinates();

    /// <summary>
    /// Disable the service required to query coordinates/anchors.
    /// </summary>
    void DisableGenericCoordinates();

}
