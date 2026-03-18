using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class InventorySystem : MonoBehaviour
{
    [Header("Inventory Configuration")]
    public Vector2Int inventorySize = new Vector2Int(8, 6);
    public float slotSize = 64f;
    public float slotSpacing = 4f;

    [Header("UI References")]
    public Canvas inventoryCanvas;
    public GameObject inventoryPanel;
    public Transform gridContainer;
    public GameObject slotPrefab;
    public GameObject itemUIPrefab;
    public Button equipButton;
    public Button eatButton;
    public Button dropButton;
    public Button closeButton;
    public TextMeshProUGUI selectedItemInfo;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pickupSound;
    public AudioClip dropSound;
    public AudioClip eatSound;
    public AudioClip equipSound;

    // Core data
    private InventoryGrid inventory;
    private InventorySlot[,] slotUI;
    private InventoryItem selectedItem;
    private bool isInventoryOpen = false;

    // Drag and drop
    private InventoryItemUI draggedItemUI;
    private Vector2Int dragStartPosition;
    private bool isDragging = false;
    [SerializeField] private bool builtInInputEnabled = true;

    // Singleton pattern
    public static InventorySystem Instance { get; private set; }

    // Events
    public System.Action<InventoryItem> OnItemAdded;
    public System.Action<InventoryItem> OnItemRemoved;
    public System.Action<InventoryItem> OnItemUsed;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (inventoryCanvas != null)
            {
                DontDestroyOnLoad(inventoryCanvas.gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // Rebind UI canvas and grid container after scene transitions
        if (inventoryCanvas == null || gridContainer == null)
        {
            // Try to find canvas in inspector-assigned reference or search scene
            if (inventoryCanvas == null)
                inventoryCanvas = GetComponentInChildren<Canvas>();

            // Try to find grid container
            if (gridContainer == null && inventoryPanel != null)
                gridContainer = inventoryPanel.GetComponentInChildren<GridLayoutGroup>()?.transform;
                
            if (gridContainer == null)
                Debug.LogWarning("InventorySystem: gridContainer not found after scene transition.");
        }
    }

    void Start()
    {
        InitializeInventory();
        SetupUI();
        SetupEventListeners();

        // Close inventory by default
        CloseInventory();
    }

    void Update()
    {
        if (!builtInInputEnabled)
            return;

        HandleInput();
    }

    void InitializeInventory()
    {
        inventory = new InventoryGrid(inventorySize.x, inventorySize.y);
        slotUI = new InventorySlot[inventorySize.x, inventorySize.y];
    }

    void SetupUI()
    {
        // Create grid layout
        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            gridLayout = gridContainer.gameObject.AddComponent<GridLayoutGroup>();

        gridLayout.cellSize = Vector2.one * slotSize;
        gridLayout.spacing = Vector2.one * slotSpacing;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = inventorySize.x;

        // Create inventory slots
        for (int y = 0; y < inventorySize.y; y++)
        {
            for (int x = 0; x < inventorySize.x; x++)
            {
                GameObject slotObj = Instantiate(slotPrefab, gridContainer);
                InventorySlot slot = slotObj.GetComponent<InventorySlot>();
                if (slot == null)
                    slot = slotObj.AddComponent<InventorySlot>();

                slot.Initialize(new Vector2Int(x, y), this);
                slotUI[x, y] = slot;
            }
        }
    }

    void SetupEventListeners()
    {
        if (equipButton != null)
            equipButton.onClick.AddListener(EquipSelectedItem);
        if (eatButton != null)
            eatButton.onClick.AddListener(EatSelectedItem);
        if (dropButton != null)
            dropButton.onClick.AddListener(DropSelectedItem);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseInventory);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && isInventoryOpen)
        {
            CloseInventory();
        }
    }

    public void ToggleInventory()
    {
        if (isInventoryOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    public void OpenInventory()
    {
        isInventoryOpen = true;
        inventoryCanvas.gameObject.SetActive(true);

        // Pause player movement
        var playerController = FindObjectOfType<OverheadController>();
        if (playerController != null)
            playerController.enabled = false;

        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshInventoryDisplay();

        Debug.Log("📦 Inventory opened!");
    }

    public void CloseInventory()
    {
        if (inventoryCanvas == null)
            return;

        isInventoryOpen = false;
        inventoryCanvas.gameObject.SetActive(false);

        // Resume player movement
        var playerController = FindObjectOfType<OverheadController>();
        if (playerController != null)
            playerController.enabled = true;

        // Hide cursor (if game uses cursor lock)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        DeselectItem();

        Debug.Log("📦 Inventory closed!");
    }

    public bool IsInventoryOpen()
    {
        return isInventoryOpen;
    }

    public void SetBuiltInInputEnabled(bool enabled)
    {
        builtInInputEnabled = enabled;
    }

    public void ResetForNewGame()
    {
        CloseInventory();
        inventory = new InventoryGrid(inventorySize.x, inventorySize.y);
        selectedItem = null;
        draggedItemUI = null;
        isDragging = false;
        builtInInputEnabled = true;
        isInventoryOpen = false;

        // Clear the UI slots and regenerate from empty grid
        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }
        SetupUI();

        Debug.Log("InventorySystem: Reset for new game.");
    }

    public bool AddMushroom(MushroomData mushroomData)
    {
        // Try to stack with existing mushroom first
        var existingItem = inventory.items.FirstOrDefault(item =>
            item.mushroomData == mushroomData &&
            item.currentStack < item.stackSize);

        if (existingItem != null)
        {
            existingItem.currentStack++;
            RefreshInventoryDisplay();
            PlaySound(pickupSound);
            OnItemAdded?.Invoke(existingItem);
            return true;
        }

        // Create new item
        InventoryItem newItem = new InventoryItem(mushroomData);
        Vector2Int? emptySpace = inventory.FindEmptySpace(newItem);

        if (emptySpace.HasValue)
        {
            inventory.PlaceItem(newItem, emptySpace.Value);
            RefreshInventoryDisplay();
            PlaySound(pickupSound);
            OnItemAdded?.Invoke(newItem);

            Debug.Log($"📦 Added {newItem.displayName} to inventory!");
            return true;
        }

        Debug.Log("📦 Inventory full! Cannot add " + mushroomData.displayName);
        return false;
    }

    public void RefreshInventoryDisplay()
    {
        // Clear all slot displays
        for (int x = 0; x < inventorySize.x; x++)
        {
            for (int y = 0; y < inventorySize.y; y++)
            {
                if (slotUI[x, y] != null)
                    slotUI[x, y].ClearItem();
            }
        }

        // Display all items
        for (int x = 0; x < inventorySize.x; x++)
        {
            for (int y = 0; y < inventorySize.y; y++)
            {
                InventoryItem item = inventory.grid[x, y];
                if (item != null)
                {
                    // Only show item UI on the top-left slot of multi-slot items
                    bool isTopLeft = true;
                    for (int checkX = x - 1; checkX >= 0 && checkX >= x - item.gridSize.x + 1; checkX--)
                    {
                        for (int checkY = y - 1; checkY >= 0 && checkY >= y - item.gridSize.y + 1; checkY--)
                        {
                            if (inventory.grid[checkX, checkY] == item)
                            {
                                isTopLeft = false;
                                break;
                            }
                        }
                        if (!isTopLeft) break;
                    }

                    if (isTopLeft)
                    {
                        slotUI[x, y].SetItem(item);
                    }
                }
            }
        }

        UpdateActionButtons();
    }

    public void SelectItem(InventoryItem item)
    {
        selectedItem = item;
        UpdateSelectedItemInfo();
        UpdateActionButtons();
    }

    public void DeselectItem()
    {
        selectedItem = null;
        if (selectedItemInfo != null)
            selectedItemInfo.text = "Select an item to see details...";
        UpdateActionButtons();
    }

    void UpdateSelectedItemInfo()
    {
        if (selectedItem == null || selectedItemInfo == null) return;

        string info = $"<b>{selectedItem.displayName}</b>\n";
        info += $"Stack: {selectedItem.currentStack}/{selectedItem.stackSize}\n";
        info += $"Type: {selectedItem.itemType}\n";
        info += $"Size: {selectedItem.gridSize.x}x{selectedItem.gridSize.y}\n\n";
        info += selectedItem.description;

        selectedItemInfo.text = info;
    }

    void UpdateActionButtons()
    {
        bool hasSelection = selectedItem != null;

        if (equipButton != null)
            equipButton.interactable = hasSelection && selectedItem.canEquip;
        if (eatButton != null)
            eatButton.interactable = hasSelection && selectedItem.canEat;
        if (dropButton != null)
            dropButton.interactable = hasSelection && selectedItem.canDrop;
    }

    /// <summary>
    /// Check if the inventory contains an item with the specified name
    /// </summary>
    /// <param name="itemName">Name to search for (checks display name, mushroom type, and mushroom display name)</param>
    /// <returns>True if item is found, false otherwise</returns>
    public bool HasItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || inventory == null)
            return false;

        // Search through all items in the inventory
        for (int x = 0; x < inventorySize.x; x++)
        {
            for (int y = 0; y < inventorySize.y; y++)
            {
                InventoryItem item = inventory.grid[x, y];
                if (item != null)
                {
                    // Check against display name first
                    if (item.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Also check against mushroom type if it's a mushroom item
                    if (item.mushroomData != null &&
                        item.mushroomData.mushroomType.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Also check against mushroom display name
                    if (item.mushroomData != null &&
                        item.mushroomData.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Check if the inventory contains at least the specified amount of an item
    /// </summary>
    /// <param name="itemName">Name to search for</param>
    /// <param name="requiredAmount">Minimum amount required</param>
    /// <returns>True if enough items are found, false otherwise</returns>
    public bool HasItem(string itemName, int requiredAmount)
    {
        if (string.IsNullOrEmpty(itemName) || inventory == null || requiredAmount <= 0)
            return false;

        int totalFound = 0;

        // Search through all items in the inventory and count matching items
        for (int x = 0; x < inventorySize.x; x++)
        {
            for (int y = 0; y < inventorySize.y; y++)
            {
                InventoryItem item = inventory.grid[x, y];
                if (item != null)
                {
                    bool isMatch = false;

                    // Check against display name first
                    if (item.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        isMatch = true;

                    // Also check against mushroom type if it's a mushroom item
                    if (!isMatch && item.mushroomData != null &&
                        item.mushroomData.mushroomType.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        isMatch = true;

                    // Also check against mushroom display name
                    if (!isMatch && item.mushroomData != null &&
                        item.mushroomData.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                        isMatch = true;

                    if (isMatch)
                    {
                        totalFound += item.currentStack;
                        if (totalFound >= requiredAmount)
                            return true;
                    }
                }
            }
        }
        return false;
    }


    public void EquipSelectedItem()
    {
        if (selectedItem == null || !selectedItem.canEquip) return;

        // Equipment logic here
        PlaySound(equipSound);
        Debug.Log($"⚔️ Equipped {selectedItem.displayName}!");
    }

    public void EatSelectedItem()
    {
        if (selectedItem == null || !selectedItem.canEat) return;

        ApplyMushroomEffects(selectedItem);

        selectedItem.currentStack--;
        if (selectedItem.currentStack <= 0)
        {
            inventory.RemoveItem(selectedItem);
            DeselectItem();
        }

        RefreshInventoryDisplay();
        PlaySound(eatSound);
        OnItemUsed?.Invoke(selectedItem);

        Debug.Log($"🍄 Ate {selectedItem.displayName}!");
    }

    void ApplyMushroomEffects(InventoryItem item)
    {
        switch (item.mushroomData.rarity)
        {
            case MushroomRarity.Common:
                Debug.Log("🔋 Restored small amount of health!");
                break;
            case MushroomRarity.Uncommon:
                Debug.Log("🔋 Restored medium amount of health!");
                break;
            case MushroomRarity.Rare:
                Debug.Log("🔋 Restored large amount of health!");
                break;
            case MushroomRarity.Epic:
                Debug.Log("⚡ Gained speed boost!");
                break;
            case MushroomRarity.Legendary:
                Debug.Log("✨ Gained mysterious power!");
                break;
        }

        // Trigger psychedelic effect for Shiitake specifically
        if (item.mushroomData != null &&
            item.mushroomData.mushroomType.Equals("Shiitake", System.StringComparison.OrdinalIgnoreCase))
        {
            if (PsychedelicEffect.Instance != null)
                PsychedelicEffect.Instance.Play();
            else
                Debug.LogWarning("PsychedelicEffect not found in scene. Add it to a persistent GameObject.");
        }
    }

    public void DropSelectedItem()
    {
        if (selectedItem == null || !selectedItem.canDrop) return;

        // Spawn mushroom in world
        SpawnMushroomInWorld(selectedItem);

        // Remove from inventory
        selectedItem.currentStack--;
        if (selectedItem.currentStack <= 0)
        {
            inventory.RemoveItem(selectedItem);
            DeselectItem();
        }

        RefreshInventoryDisplay();
        PlaySound(dropSound);
        OnItemRemoved?.Invoke(selectedItem);

        Debug.Log($"📤 Dropped {selectedItem.displayName}!");
    }

    void SpawnMushroomInWorld(InventoryItem item)
    {
        if (item.mushroomData.mushroomPrefab == null) return;

        // Find player position
        Transform player = FindObjectOfType<OverheadController>()?.transform;
        if (player == null) return;

        // Spawn slightly in front of player
        Vector3 spawnPos = player.position + player.forward * 2f;

        // Raycast to ground
        if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
        {
            spawnPos = hit.point;
        }

        GameObject dropped = Instantiate(item.mushroomData.mushroomPrefab, spawnPos, Quaternion.identity);

        // Add pickup component if it doesn't exist
        if (dropped.GetComponent<MushroomPickup>() == null)
        {
            MushroomPickup pickup = dropped.AddComponent<MushroomPickup>();
            pickup.mushroomData = item.mushroomData;
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // Drag and drop methods (called by InventorySlot)
    public void StartDragging(InventoryItemUI itemUI, InventoryItem item, Vector2Int startPos)
    {
        draggedItemUI = itemUI;
        dragStartPosition = startPos;
        isDragging = true;

        // Make dragged item follow cursor
        itemUI.SetDragging(true);
    }

    public void UpdateDragging(Vector2 mousePosition)
    {
        if (isDragging && draggedItemUI != null)
        {
            draggedItemUI.transform.position = mousePosition;
        }
    }

    public void StopDragging(Vector2Int targetPosition)
    {
        if (!isDragging || selectedItem == null) return;

        isDragging = false;

        // Check if we can place item at target position
        if (targetPosition != dragStartPosition)
        {
            // Temporarily remove item from old position
            inventory.RemoveItem(selectedItem);

            // Try to place at new position
            if (inventory.CanPlaceItem(selectedItem, targetPosition))
            {
                inventory.PlaceItem(selectedItem, targetPosition);
                RefreshInventoryDisplay();
                Debug.Log($"📦 Moved {selectedItem.displayName}");
            }
            else
            {
                // Place back at original position
                inventory.PlaceItem(selectedItem, dragStartPosition);
                RefreshInventoryDisplay();
                Debug.Log($"📦 Cannot place {selectedItem.displayName} there");
            }
        }

        if (draggedItemUI != null)
        {
            draggedItemUI.SetDragging(false);
            draggedItemUI = null;
        }
    }

    public InventoryItem GetItemAt(Vector2Int position)
    {
        if (position.x >= 0 && position.x < inventorySize.x &&
            position.y >= 0 && position.y < inventorySize.y)
        {
            return inventory.grid[position.x, position.y];
        }
        return null;
    }
}
