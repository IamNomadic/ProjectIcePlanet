// InventoryUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all UI aspects of the inventory. It listens to the Inventory data script
/// and updates the visual representation accordingly. Handles user input for interacting with the UI.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // --- Inspector References ---
    [Header("Component References")]
    [Tooltip("The Inventory data backend this UI should display.")]
    [SerializeField] private Inventory _inventory;
    public Inventory BackendInventory => _inventory;
    [Tooltip("The parent object for all inventory slot buttons.")]
    [SerializeField] private GameObject _itemButtonPanel;
    [Tooltip("The prefab for an individual inventory item button.")]
    [SerializeField] private ItemButton _itemButtonPrefab;
    [Tooltip("A reference to the pause menu logic.")]
    [SerializeField] private PauseMenu _pauseMenu;

    [Header("Item Info Box")]
    [Tooltip("The parent GameObject for the item information display.")]
    [SerializeField] private GameObject _itemInfoBoxHolder;
    [SerializeField] private TMP_Text _txtName;
    [SerializeField] private TMP_Text _txtDescription;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Button _useButton;
    [SerializeField] private Button _dropButton;

    [Header("Drag & Drop Visuals")]
    [SerializeField] private Image _draggedItemIcon;
    [SerializeField] private TMP_Text _draggedItemQuantityText;

    [Header("Notifications & Other UI")]
    [Tooltip("The button used to open the inventory. Its sprite can be changed for notifications.")]
    [SerializeField] private Button _inventoryButton;
    [SerializeField] private Sprite _itemAddedSprite;
    [SerializeField] private Sprite _defaultInventorySprite;

    [Header("Game Panels")]
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _settingsPanel;



    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // --- Static Fields ---

    /// <summary>
    /// Represents the item slot currently being dragged by the cursor.
    /// Static to be accessible from ItemButton instances for drag operations.
    /// </summary>
    public static InventorySlot cursorSlot = new InventorySlot(null, 0);

    /// <summary>
    /// Which Inventory did the current cursor item come from (origin).
    /// Set by ItemButton when a drag begins; cleared when cursorSlot is emptied.
    /// </summary>
    public static Inventory cursorOriginInventory = null;

    /// <summary>
    /// Which slot index in the origin inventory the current cursor item came from.
    /// </summary>
    public static int cursorOriginIndex = -1;

    /// <summary>
    /// Global registry of all InventoryUI instances in the scene (active or enabled).
    /// Used to find and refresh UIs bound to a specific Inventory backend.
    /// </summary>
    private static readonly List<InventoryUI> s_allInventoryUIs = new List<InventoryUI>();

    // --- Private State ---
    private ItemButton _selectedButtonForInfo;
    private ItemButton _selectedItemForCraft;
    private List<ItemButton> _currentButtons = new List<ItemButton>();
    private bool _isCraftingMode = false;

    // --- Unity Lifecycle Methods ---

    private void Awake()
    {
        if (_useButton != null) _useButton.onClick.AddListener(OnUseButtonClicked);
        if (_dropButton != null) _dropButton.onClick.AddListener(OnDropButtonClicked);
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] Awake called. Backend: {_inventory?.name ?? "null"}");
    }

    private void OnEnable()
    {
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] OnEnable registering instance.");
        if (!s_allInventoryUIs.Contains(this)) s_allInventoryUIs.Add(this);

        SubscribeToBackend();
        ItemButton.OnButtonClicked += OnItemButtonClicked;

        RefreshUI();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayOpenSound();
    }

    private void OnDisable()
    {
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] OnDisable unregistering instance.");
        s_allInventoryUIs.Remove(this);

        UnsubscribeFromBackend();
        ItemButton.OnButtonClicked -= OnItemButtonClicked;

        if (_isCraftingMode) _isCraftingMode = false;
        ClearCraftingSelection();

        if (AudioManager.Instance != null) AudioManager.Instance.PlayCloseSound();
    }

    private void Update()
    {
        HandleShiftCraftingDeselect();

        if (cursorSlot.item != null)
        {
            UpdateDraggedIconPosition();
        }
    }

    /// <summary>
    /// Allows runtime assignment of the Inventory data source.
    /// Subscribes to the new backend immediately and refreshes the UI.
    /// Ensures this InventoryUI is registered globally so RefreshIfBackend can find it.
    /// </summary>
    public void SetBackendInventory(Inventory inventoryBackend)
    {
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] SetBackendInventory called. New backend: {inventoryBackend?.name ?? "null"}");

        UnsubscribeFromBackend();

        _inventory = inventoryBackend;

        SubscribeToBackend();

        if (!s_allInventoryUIs.Contains(this)) s_allInventoryUIs.Add(this);
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] Registered in global UI list (count={s_allInventoryUIs.Count}).");

        RefreshUI();
    }

    // --- Backend subscription helpers ---

    private void SubscribeToBackend()
    {
        if (_inventory == null)
        {
            if (_debugMode) Debug.LogWarning($"[InventoryUI:{gameObject.name}] SubscribeToBackend skipped: _inventory is null.");
            return;
        }

        UnsubscribeFromBackend();

        _inventory.OnInventoryUpdated += RefreshUI;
        _inventory.OnItemAdded += OnItemAdded;
        _inventory.OnItemUseFailed += OnActionFailed;
        _inventory.OnItemDropFailed += OnActionFailed;

        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] Subscribed to backend events for {_inventory.name}.");
    }

    private void UnsubscribeFromBackend()
    {
        if (_inventory == null) return;

        _inventory.OnInventoryUpdated -= RefreshUI;
        _inventory.OnItemAdded -= OnItemAdded;
        _inventory.OnItemUseFailed -= OnActionFailed;
        _inventory.OnItemDropFailed -= OnActionFailed;

        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] Unsubscribed from backend events for {_inventory.name}.");
    }

    /// <summary>
    /// Refresh all InventoryUI instances that are bound to the specified backend inventory.
    /// Safe to call from ItemButton after changing inventory data.
    /// </summary>
    public static void RefreshIfBackend(Inventory backend)
    {
        if (backend == null) return;

        for (int i = 0; i < s_allInventoryUIs.Count; i++)
        {
            var ui = s_allInventoryUIs[i];
            if (ui == null) continue;
            if (ui._inventory == backend)
            {
                if (ui._debugMode) Debug.Log($"[InventoryUI:{ui.gameObject.name}] RefreshIfBackend matched backend {backend.name}; calling RefreshUI.");
                ui.RefreshUI();
            }
        }
    }

    // --- Public UI Control Methods ---

    public void ToggleInventoryPanel()
    {
        if (_inventoryPanel == null) return;
        _inventoryPanel.SetActive(!_inventoryPanel.activeSelf);
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] ToggleInventoryPanel -> {_inventoryPanel.activeSelf}");
    }

    public void TogglePauseMenu()
    {
        if (_pausePanel == null || _settingsPanel == null || _pauseMenu == null) return;

        if (_pausePanel.activeSelf || _settingsPanel.activeSelf)
        {
            _pauseMenu.ResumeGame();
            _pausePanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }
        else
        {
            _pauseMenu.Pause();
            _pausePanel.SetActive(true);
        }
    }

    public void ToggleCraftMode()
    {
        _isCraftingMode = !_isCraftingMode;

        ResetUIState();
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] Crafting mode is now: " + (_isCraftingMode ? "ON" : "OFF"));
    }

    public void UpdateCursorIcon()
    {
        if (_draggedItemIcon == null || _draggedItemQuantityText == null) return;

        if (cursorSlot.item != null)
        {
            _draggedItemIcon.gameObject.SetActive(true);
            _draggedItemIcon.sprite = cursorSlot.item.icon;

            bool showQuantity = cursorSlot.quantity > 1;
            _draggedItemQuantityText.gameObject.SetActive(showQuantity);
            if (showQuantity)
            {
                _draggedItemQuantityText.text = cursorSlot.quantity.ToString();
            }
        }
        else
        {
            _draggedItemIcon.gameObject.SetActive(false);
        }

        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] UpdateCursorIcon called. Cursor: {cursorSlot.item?.itemName ?? "empty"} x{cursorSlot.quantity}");
    }

    // --- Event Handlers ---

    private void OnItemAdded(ItemSO newItem)
    {
        if (_inventoryButton != null && _itemAddedSprite != null)
        {
            _inventoryButton.image.sprite = _itemAddedSprite;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCraftedSound();
        }

        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] OnItemAdded called for {newItem?.itemName ?? "null"}");
    }

    private void OnActionFailed()
    {


        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] OnActionFailed called.");
    }

    private void OnItemButtonClicked(ItemButton clickedButton)
    {
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

       
            PopulateItemInfo(clickedButton);
        

        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] OnItemButtonClicked: slot {clickedButton?.slotIndex}");
    }

    private void OnUseButtonClicked()
    {
        if (_selectedButtonForInfo == null || _inventory == null) return;
        _inventory.UseItem(_selectedButtonForInfo.slotIndex);
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] UseItem called on slot {_selectedButtonForInfo.slotIndex}");
    }

    private void OnDropButtonClicked()
    {
        if (_selectedButtonForInfo == null || _inventory == null) return;
        _inventory.DropItem(_selectedButtonForInfo.slotIndex);
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] DropItem called on slot {_selectedButtonForInfo.slotIndex}");
    }

    // --- UI Logic ---

    private void RefreshUI()
    {
        if (_itemButtonPanel == null || _itemButtonPrefab == null || _inventory == null)
        {
            if (_debugMode) Debug.LogWarning($"[InventoryUI:{gameObject.name}] Cannot RefreshUI: missing references or backend.");
            return;
        }

        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] RefreshUI rebuilding buttons for backend {_inventory.name}.");

        foreach (Transform child in _itemButtonPanel.transform)
            Destroy(child.gameObject);
        _currentButtons.Clear();

        for (int i = 0; i < _inventory.Slots.Length; i++)
        {
            var btn = Instantiate(_itemButtonPrefab, _itemButtonPanel.transform);
            btn.slotIndex = i;
            btn.Initialize(_inventory.GetSlot(i), this);
            _currentButtons.Add(btn);
        }

        ResetUIState();
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] UI Refreshed. Buttons: {_currentButtons.Count}");
    }

    private void PopulateItemInfo(ItemButton clickedButton)
    {
        if (clickedButton == null) return;

        if (_selectedButtonForInfo == clickedButton)
        {
            ResetUIState();
            return;
        }

        _selectedButtonForInfo = clickedButton;
        var data = clickedButton.GetItem();
        if (data == null) return;

        if (_itemInfoBoxHolder != null) _itemInfoBoxHolder.SetActive(true);
        if (_txtName != null) _txtName.text = data.itemName;
        if (_txtDescription != null) _txtDescription.text = data.itemDescription;
        if (data.icon != null && _itemIcon != null) _itemIcon.sprite = data.icon;

        if (_useButton != null) _useButton.gameObject.SetActive(true);
        if (_dropButton != null) _dropButton.gameObject.SetActive(true);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySelectedSound();
        if (_debugMode) Debug.Log($"[InventoryUI:{gameObject.name}] PopulateItemInfo slot {clickedButton.slotIndex} -> {data.itemName}");
    }

    // --- Crafting Logic ---



  
    // --- Helper Methods ---

    private void ClearNotification()
    {
        if (_inventoryButton != null && _defaultInventorySprite != null)
        {
            _inventoryButton.image.sprite = _defaultInventorySprite;
        }
    }

    private void UpdateDraggedIconPosition()
    {
        if (_draggedItemIcon != null)
            _draggedItemIcon.transform.position = Input.mousePosition;
    }

    private void ClearCraftingSelection()
    {
        if (_selectedItemForCraft != null)
        {
            _selectedItemForCraft.SetHighlight(false);
        }
        _selectedItemForCraft = null;

        foreach (var btn in _currentButtons)
        {
            if (btn.GetItem() != null)
            {
                btn.SetCraftingAvailability(true);
            }
        }
    }

    private void HandleShiftCraftingDeselect()
    {
        if (!_isCraftingMode)
        {
            bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!isShiftHeld && _selectedItemForCraft != null)
            {
                ClearCraftingSelection();
            }
        }
    }

    private void ResetUIState(bool keepCraftSelection = false)
    {
        if (_itemInfoBoxHolder != null) _itemInfoBoxHolder.SetActive(false);
        if (_useButton != null) _useButton.gameObject.SetActive(false);
        if (_dropButton != null) _dropButton.gameObject.SetActive(false);
        _selectedButtonForInfo = null;

        if (!keepCraftSelection)
        {
            ClearCraftingSelection();
        }
    }
}
