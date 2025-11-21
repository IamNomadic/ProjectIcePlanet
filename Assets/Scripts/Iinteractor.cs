using UnityEngine;

/// <summary>
/// Defines an entity that can interact with IInteractable objects.
/// </summary>
public interface IInteractor
{
    /// <summary>
    /// A reference to the Inventory system of the interactor.
    /// </summary>
    Inventory Inventory { get; }

    /// <summary>
    /// The GameObject of the interactor.
    /// </summary>
    GameObject gameObject { get; }
}