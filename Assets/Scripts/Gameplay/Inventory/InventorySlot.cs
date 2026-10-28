using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Vampire
{
    public class InventorySlot : MonoBehaviour
    {
        [field: SerializeField] public CollectableType CollectableType;
        [SerializeField] private GameObject countObject;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Image itemImage;
        private List<Collectable> items;
        private FastList<Collectable> itemsBeingAdded;
        public int Count => items != null ? items.Count : 0;
        public int Capacity => CollectableType != null ? Mathf.Max(0, CollectableType.inventoryStackSize) : 0;
        public string CountLabel => $"{Count}/{Capacity}";
        public string CapacityDescription => $"보유 {Count} / 최대 {Capacity}개" + (Capacity > 0 && Count >= Capacity ? " · 최대 보유량" : "");
        public Image IconImage => itemImage;
        public RectTransform CountRect => countObject != null ? countObject.transform as RectTransform : null;
        public bool IsBomb => CollectableType is BombCollectable;
        int lastBombFrame=-1;

        public void Init()
        {
            items = new List<Collectable>();
            itemsBeingAdded = new FastList<Collectable>();
            countObject.SetActive(false);
            countText.text = CountLabel;
            itemImage.color = new Color(1, 1, 1, 0.5f);
            if(IsBomb)
            {
                var button=GetComponent<Button>();
                if(button!=null){button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(UsePointerItem);}
            }
        }

        public void AddItemBeingCollected(Collectable item)
        {
            itemsBeingAdded.Add(item);
        }

        public void FinalizeAddItemBeingCollected(Collectable item)
        {
            if (itemsBeingAdded.Contains(item))
                itemsBeingAdded.Remove(item);
            AddItem(item);
        }
        public void CancelItemBeingCollected(Collectable item)
        {
            if (itemsBeingAdded != null && itemsBeingAdded.Contains(item)) itemsBeingAdded.Remove(item);
        }

        public void AddItem(Collectable item)
        {
            items.Add(item);
            countText.text = CountLabel;
            if (!countObject.activeInHierarchy)
            {
                countObject.SetActive(true);
                itemImage.color = Color.white;
            }
        }

        public void UseInputItem(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if(!context.performed || Time.timeScale<=0 || GameInput.KeyboardBlocked)return;
            if(IsBomb)BombTargeting.For(this)?.ThrowAutomatic(this);else UseItem();
        }

        public void UseItem()
        {
            // Serialized PlayerInput callbacks can arrive for multiple phases. Only a fresh key press throws.
            if(IsBomb)
            {
                if(GameInput.Pressed(GameKeyAction.Slot1))BombTargeting.For(this)?.ThrowAutomatic(this);
                return;
            }
            if (items.Count > 0)
            {
                items[0].Use();
                items.RemoveAt(0);
                countText.text = CountLabel;

                if (items.Count == 0)
                {
                    countObject.SetActive(false);
                    itemImage.color = new Color(1, 1, 1, 0.5f);
                }
            }
        }

        public void UsePointerItem()
        {
            if(IsBomb)BombTargeting.For(this)?.BeginAim(this);else UseItem();
        }
        public bool ThrowBomb(Vector2 target)
        {
            if(!IsBomb||Count==0||lastBombFrame==Time.frameCount||!(items[0] is Bomb bomb))return false;
            if(!bomb.ThrowAt(target))return false;
            lastBombFrame=Time.frameCount;items.RemoveAt(0);countText.text=CountLabel;
            countObject.SetActive(Count>0);itemImage.color=new Color(1,1,1,Count>0?1:.5f);return true;
        }

        public bool IsFull()
        {
            return items.Count + itemsBeingAdded.Count >= CollectableType.inventoryStackSize;
        }
    }
}
