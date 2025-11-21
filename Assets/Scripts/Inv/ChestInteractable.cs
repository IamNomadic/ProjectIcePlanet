using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Inventory))]
public class ChestInteractable : MonoBehaviour, IInteractable
{
    [Header("Chest UI Panel")]
    [Tooltip("Root GameObject of the chest's InventoryUI (inactive at start).")]
    [SerializeField] private GameObject _chestUIPanel;

    private Inventory _chestInventory;
    private InventoryUI _chestUIController;

    // Track current interactor(s) in range so we only auto-close when the player leaves
    // (multiple interactor types supported)
    private readonly HashSet<Collider2D> _interactorsInRange = new HashSet<Collider2D>();

    private void Awake()
    {
        _chestInventory = GetComponent<Inventory>();
        _chestUIController = _chestUIPanel.GetComponent<InventoryUI>();
        _chestUIController.SetBackendInventory(_chestInventory);
        _chestUIPanel.SetActive(false);
    }

    public string InteractionPrompt => "Open Chest";

    // Called by PlayerInteractor when the player attempts to interact (touch tap or key press).
    // This method will open the UI (idempotent) and return true to indicate the interaction succeeded.
    public bool Interact(IInteractor interactor)
    {
        Open();
        return true;
    }

    // Explicit open/close so different input flows don't toggle unexpectedly.
    public void Open()
    {
        if (_chestUIPanel == null) return;
        if (!_chestUIPanel.activeSelf)
        {
            _chestUIPanel.SetActive(true);
            // Optionally: notify AudioManager, Animation, etc.
        }
    }

    public void Close()
    {
        if (_chestUIPanel == null) return;
        if (_chestUIPanel.activeSelf)
        {
            _chestUIPanel.SetActive(false);
            // Optionally: notify AudioManager, Animation, etc.
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // track anything that could interact (player collider etc.)
        _interactorsInRange.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // remove leaving collider; if nothing interactive remains in range, auto-close
        _interactorsInRange.Remove(other);

        if (_interactorsInRange.Count == 0)
        {
            Close();
        }
    }
}
