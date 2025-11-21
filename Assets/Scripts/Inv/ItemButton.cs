// ItemButton.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Represents a single clickable, draggable button in the inventory UI.
/// It handles all direct user input for a slot, such as clicking, dragging, and dropping,
/// and communicates changes to the Inventory and InventoryUI scripts.
/// </summary>
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class ItemButton : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    // --- Events ---
    public static event Action<ItemButton> OnButtonClicked;

    // --- Inspector References ---
    [Header("UI Elements")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private GameObject _highlightOverlay;
    [SerializeField] private TMP_Text _quantityText;

    [Header("Hold-to-Drag Settings")]
    [Tooltip("Seconds to hold before auto–dragging half the stack.")]
    [SerializeField] private float _holdThreshold = 0.5f;

    // --- Public Properties ---
    public int slotIndex { get; set; }

    // --- Private Fields ---
    private ItemSO _itemData;
    private Inventory _inventory;
    private InventoryUI _inventoryUI;
    private CanvasGroup _canvasGroup;

    private bool _pointerDown;
    private float _pointerDownTimer;
    private bool _hasTriggeredHold;
    public bool _debugMode;

    // --- Unity Lifecycle Methods ---
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        if (_pointerDown && !_hasTriggeredHold)
        {
            _pointerDownTimer += Time.unscaledDeltaTime;
            if (_pointerDownTimer >= _holdThreshold)
            {
                _hasTriggeredHold = true;
                BeginHalfStackDrag();
            }
        }
    }

    // --- Initialization ---
    public void Initialize(InventorySlot slot, InventoryUI inventoryUI)
    {
        _inventoryUI = inventoryUI;
        _inventory = inventoryUI.BackendInventory;
        _itemData = slot.item;

        // DIAGNOSTIC LOG: confirm which backend this button references
        if(_debugMode)
        Debug.Log($"[ItemButton:{gameObject.name}] Initialize -> backend: {_inventory?.name ?? "null"}, slotIndex: {slotIndex}");

        if (_itemData != null)
        {
            if (_itemIcon != null) _itemIcon.sprite = _itemData.icon;
            if (_itemIcon != null) _itemIcon.enabled = true;
            SetQuantity(slot.quantity);
        }
        else
        {
            if (_itemIcon != null) _itemIcon.enabled = false;
            if (_quantityText != null) _quantityText.gameObject.SetActive(false);
        }
    }
    // --- Input Handlers ---
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left
            && _itemData != null
            && InventoryUI.cursorSlot.item == null)
        {
            _pointerDown = true;
            _pointerDownTimer = 0f;
            _hasTriggeredHold = false;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDown = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InventoryUI.cursorSlot.item != null)
        {
            HandlePlaceItem();
        }
        else if (eventData.button == PointerEventData.InputButton.Left && _itemData != null)
        {
            OnButtonClicked?.Invoke(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        InventorySlot sourceSlot = _inventory.GetSlot(slotIndex);

        if (_hasTriggeredHold)
        {
            // We already picked up half the stack in the hold logic
            _canvasGroup.blocksRaycasts = false;
            _inventoryUI.UpdateCursorIcon();
            return;
        }

        if (sourceSlot.item == null || InventoryUI.cursorSlot.item != null) return;

        // record origin for cross-UI refreshes
        InventoryUI.cursorOriginInventory = _inventory;
        InventoryUI.cursorOriginIndex = slotIndex;

        // --- LEFT-DRAG: PICK UP FULL STACK ---
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            InventoryUI.cursorSlot = new InventorySlot(sourceSlot.item, sourceSlot.quantity);
            _inventory.ClearSlot(slotIndex); // this invokes source inventory's OnInventoryUpdated
        }
        // --- RIGHT-DRAG: PICK UP HALF STACK ---
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (sourceSlot.quantity > 1)
            {
                int halfQuantity = Mathf.CeilToInt(sourceSlot.quantity / 2f);
                int remainingQuantity = sourceSlot.quantity - halfQuantity;

                InventoryUI.cursorSlot = new InventorySlot(sourceSlot.item, halfQuantity);
                _inventory.UpdateSlotQuantity(slotIndex, remainingQuantity); // invokes source inventory update
            }
            else
            {
                InventoryUI.cursorSlot = new InventorySlot(sourceSlot.item, sourceSlot.quantity);
                _inventory.ClearSlot(slotIndex); // invokes update
            }
        }

        _canvasGroup.blocksRaycasts = false;
        _inventoryUI.UpdateCursorIcon();
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Restore button to block raycasts
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (InventoryUI.cursorSlot.item != null)
        {
            HandlePlaceItem();
        }
    }

    // --- Public UI Methods ---
    public ItemSO GetItem() => _itemData;
    public void SetHighlight(bool highlighted)
    {
        if (_highlightOverlay != null) _highlightOverlay.SetActive(highlighted);
    }

    public void SetCraftingAvailability(bool available)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = available ? 1f : 0.25f;
        _canvasGroup.interactable = available;
    }

    // --- Private Helper Methods ---
    private void SetQuantity(int quantity)
    {
        bool showQuantity = quantity > 1;
        if (_quantityText != null) _quantityText.gameObject.SetActive(showQuantity);
        if (showQuantity && _quantityText != null) _quantityText.text = quantity.ToString();
    }

    private void HandlePlaceItem()
    {
        InventorySlot clickedSlot = _inventory.GetSlot(slotIndex);

        // We'll capture origin and target for clear logging/refresing
        Inventory targetInventory = _inventory;
        Inventory originInventory = InventoryUI.cursorOriginInventory;

        if (_debugMode) Debug.Log($"[ItemButton:{gameObject.name}] HandlePlaceItem called. Target backend: {targetInventory?.name ?? "null"}, Origin backend: {originInventory?.name ?? "null"}");

        // Case 1: Clicked slot is empty
        if (clickedSlot.item == null)
        {
            _inventory.AssignSlot(slotIndex, InventoryUI.cursorSlot); // invokes target inventory update

            // Clear cursor (but don't clear origin tracking until after refresh)
            InventoryUI.cursorSlot = new InventorySlot(null, 0);
        }
        // Case 2: Clicked slot has the SAME item (Merge)
        else if (clickedSlot.item == InventoryUI.cursorSlot.item)
        {
            int spaceAvailable = clickedSlot.item.maxStackSize - clickedSlot.quantity;
            int amountToTransfer = Mathf.Min(spaceAvailable, InventoryUI.cursorSlot.quantity);

            if (amountToTransfer > 0)
            {
                _inventory.UpdateSlotQuantity(slotIndex, clickedSlot.quantity + amountToTransfer); // target update
                InventoryUI.cursorSlot.quantity -= amountToTransfer;
            }

            if (InventoryUI.cursorSlot.quantity <= 0)
            {
                InventoryUI.cursorSlot = new InventorySlot(null, 0);
            }
        }
        // Case 3: Clicked slot has a DIFFERENT item (Swap)
        else
        {
            InventorySlot tempSlot = new InventorySlot(clickedSlot.item, clickedSlot.quantity);

            // Place cursor into the clicked slot (target)
            _inventory.AssignSlot(slotIndex, InventoryUI.cursorSlot); // target update

            // Cursor now holds the previous contents of the clicked slot
            InventoryUI.cursorSlot = tempSlot;
        }

        // Update cursor visuals
        _inventoryUI.UpdateCursorIcon();

        // Force refresh on target backend UIs
        InventoryUI.RefreshIfBackend(targetInventory);
        if (_debugMode) Debug.Log($"[ItemButton:{gameObject.name}] Called RefreshIfBackend for target: {targetInventory?.name ?? "null"}");

        // Also refresh the origin backend UIs if different from target and not null
        if (originInventory != null && originInventory != targetInventory)
        {
            InventoryUI.RefreshIfBackend(originInventory);
            if (_debugMode) Debug.Log($"[ItemButton:{gameObject.name}] Called RefreshIfBackend for origin: {originInventory.name}");
        }

        // Finally, if cursor is empty now, clear origin tracking
        if (InventoryUI.cursorSlot.item == null)
        {
            InventoryUI.cursorOriginInventory = null;
            InventoryUI.cursorOriginIndex = -1;
            if (_debugMode) Debug.Log($"[ItemButton:{gameObject.name}] Cursor emptied; cleared origin tracking.");
        }
    }


    private void BeginHalfStackDrag()
    {
        InventorySlot sourceSlot = _inventory.GetSlot(slotIndex);
        if (sourceSlot.item == null) return;

        // record origin for cross-UI refreshes
        InventoryUI.cursorOriginInventory = _inventory;
        InventoryUI.cursorOriginIndex = slotIndex;

        int halfQty = Mathf.CeilToInt(sourceSlot.quantity / 2f);
        int remainder = sourceSlot.quantity - halfQty;

        InventoryUI.cursorSlot = new InventorySlot(sourceSlot.item, halfQty);

        if (remainder > 0)
            _inventory.UpdateSlotQuantity(slotIndex, remainder); // invokes source update
        else
            _inventory.ClearSlot(slotIndex); // invokes source update

        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
        _inventoryUI.UpdateCursorIcon();
    }
}
