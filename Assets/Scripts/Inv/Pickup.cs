using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour, IInteractable
{
    [Tooltip("The list of items this pickup will grant upon interaction.")]
    [SerializeField] public List<ItemSO> itemsToGive;



    public bool Interact(IInteractor interactor)
    {
     
        // 3) Default pickup logic
        if (interactor.Inventory == null || itemsToGive.Count == 0)
            return false;

        var groups = itemsToGive.GroupBy(item => item);
        foreach (var group in groups)
        {
            var item = group.Key;
            var count = group.Count();
            if (!interactor.Inventory.HasSpaceFor(item, count))
            {
               
                return false;
            }
        }

        foreach (var item in itemsToGive)
            interactor.Inventory.AddItem(item);
        DestroyParentIfTagged("Corpse");
        Destroy(gameObject);
        return true;
    }
    void DestroyParentIfTagged(string targetTag)
    {
        if (transform.parent != null) // make sure it has a parent
        {
            Transform parent = transform.parent;
            if (parent.CompareTag(targetTag))
            {
                Destroy(parent.gameObject);
                Debug.Log($"Destroyed parent with tag {targetTag}");
            }
        }
    }
}
