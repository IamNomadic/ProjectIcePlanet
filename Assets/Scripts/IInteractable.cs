using UnityEngine;

/// <summary>
/// Defines an object that can be interacted with by an IInteractor.
/// </summary>
public interface IInteractable
{


    /// <summary>
    /// The method called when an interactor performs the interaction.
    /// </summary>
    /// <param name="interactor">The interactor performing the action.</param>
    /// <returns>True if the interaction was successful, false otherwise.</returns>
    bool Interact(IInteractor interactor);
}