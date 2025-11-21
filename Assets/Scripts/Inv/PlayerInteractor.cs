// PlayerInteractor.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the player's ability to interact with IInteractable objects in the world.
/// It detects nearby interactables and provides a method to trigger their interaction.
/// Now only the closest interactable (e.g. chest) will be activated per key press.
/// </summary>
public class PlayerInteractor : MonoBehaviour, IInteractor
{
    // --- Inspector References ---
    [Header("Component References")]
    [Tooltip("The player's inventory component.")]
    [SerializeField]
    private Inventory _inventory;

    [Header("UI Feedback")]
    [Tooltip("The UI Button that provides visual feedback for interaction range.")]
    [SerializeField]
    private Button _interactionButton;

    [Tooltip("The sprite to display when an item is in interaction range.")]
    [SerializeField]
    private Sprite _itemInRangeSprite;

    [Tooltip("The sprite to display when no items are in range.")]
    [SerializeField]
    private Sprite _itemNotInRangeSprite;

    // --- Private Fields ---
    private readonly List<IInteractable> _interactablesInRange = new List<IInteractable>();

    // --- IInteractor ---
    public Inventory Inventory => _inventory;

    // --- Unity Lifecycle ---
    private void Awake()
    {
        if (_inventory == null)
            _inventory = GetComponentInParent<Inventory>();
    }

    private void Update()
    {
        UpdateInteractionVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (!_interactablesInRange.Contains(interactable))
                _interactablesInRange.Add(interactable);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
            _interactablesInRange.Remove(interactable);
    }

    // --- Public Methods ---
    /// <summary>
    /// Attempts to interact with the closest IInteractable in range.
    /// Stops after the first successful interaction.
    /// </summary>
    public void TryInteract()
    {
        if (_interactablesInRange.Count == 0)
        {
            Debug.Log("No items in range to interact with.");
            return;
        }

        // Order the list by distance to this interactor
        var ordered = _interactablesInRange
            .Select(i => new { Target = i, MB = i as MonoBehaviour })
            .Where(x => x.MB != null)
            .OrderBy(x => (x.MB.transform.position - transform.position).sqrMagnitude)
            .Select(x => x.Target);

        // Only interact with the first one that returns true
        foreach (var interactable in ordered)
        {
            if (interactable.Interact(this))
                break;
        }
    }

    // --- Private Helpers ---
    private void UpdateInteractionVisuals()
    {
        if (_interactionButton == null) return;

        bool hasNearby = _interactablesInRange.Count > 0;
        _interactionButton.image.sprite = hasNearby
            ? _itemInRangeSprite
            : _itemNotInRangeSprite;
    }
}
