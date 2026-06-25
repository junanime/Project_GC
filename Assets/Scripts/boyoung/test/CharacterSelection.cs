using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class CharacterSelection : MonoBehaviour
    {
        [System.Serializable]
        public struct CharacterData
        {
            [Tooltip("CharacterBlueprint used by the game scene for this character.")]
            public CharacterBlueprint characterBlueprint;

            [Tooltip("캐릭터 이름입니다. UI의 이름 텍스트에 표시됩니다.")]
            public string charName;

            [Tooltip("캐릭터 선택 화면에 표시할 캐릭터 이미지입니다.")]
            public Sprite charSprite;

            [TextArea]
            [Tooltip("캐릭터 설명입니다. UI의 설명 텍스트에 표시됩니다.")]
            public string description;

            [Range(0, 5)]
            [Tooltip("공격력 수치입니다. 공격력 슬라이더에 표시됩니다.")]
            public int attackStat;

            [Range(0, 5)]
            [Tooltip("방어력 수치입니다. 방어력 슬라이더에 표시됩니다.")]
            public int defenseStat;

            [Range(0, 5)]
            [Tooltip("속도 수치입니다. 속도 슬라이더에 표시됩니다.")]
            public int speedStat;
        }

        [Header("Character Data")]
        [Tooltip("선택 가능한 캐릭터 데이터 배열입니다. 인스펙터에서 캐릭터 이름, 이미지, 설명, 능력치를 입력합니다.")]
        public CharacterData[] characters;

        [Header("UI Elements - Center")]
        [Tooltip("현재 선택된 중앙 캐릭터 이미지를 표시하는 Image입니다.")]
        public Image centerCharImage;

        [Tooltip("현재 선택된 캐릭터 이름을 표시하는 TextMeshProUGUI입니다.")]
        public TextMeshProUGUI nameText;

        [Tooltip("현재 선택된 캐릭터 설명을 표시하는 TextMeshProUGUI입니다.")]
        public TextMeshProUGUI descText;

        [Tooltip("현재 선택된 캐릭터의 공격력 수치를 표시하는 Slider입니다.")]
        public Slider attackSlider;

        [Tooltip("현재 선택된 캐릭터의 방어력 수치를 표시하는 Slider입니다.")]
        public Slider defenseSlider;

        [Tooltip("현재 선택된 캐릭터의 속도 수치를 표시하는 Slider입니다.")]
        public Slider speedSlider;

        [Header("UI Elements - Left/Right Preview")]
        [Tooltip("왼쪽에 표시할 이전 캐릭터 미리보기 이미지입니다.")]
        public Image leftPreviewImage;

        [Tooltip("왼쪽에 표시할 이전 캐릭터 이름 텍스트입니다.")]
        public TextMeshProUGUI leftNameText;

        [Tooltip("오른쪽에 표시할 다음 캐릭터 미리보기 이미지입니다.")]
        public Image rightPreviewImage;

        [Tooltip("오른쪽에 표시할 다음 캐릭터 이름 텍스트입니다.")]
        public TextMeshProUGUI rightNameText;

        [Tooltip("이전 캐릭터가 있을 때만 켜지는 왼쪽 카드 오브젝트입니다.")]
        public GameObject leftCard;

        [Tooltip("다음 캐릭터가 있을 때만 켜지는 오른쪽 카드 오브젝트입니다.")]
        public GameObject rightCard;

        [Header("Current Selection")]
        [SerializeField]
        [Tooltip("현재 선택된 캐릭터 인덱스입니다. 0부터 시작합니다.")]
        private int currentIndex = 0;

        public CharacterBlueprint CurrentCharacterBlueprint
        {
            get
            {
                if (characters == null || characters.Length == 0)
                    return null;

                ClampCurrentIndex();
                return characters[currentIndex].characterBlueprint;
            }
        }

        private void Start()
        {
            ClampCurrentIndex();
            UpdateCharacterUI();
        }

        [Tooltip("오른쪽 버튼에서 호출합니다. 다음 캐릭터로 이동합니다.")]
        public void OnClickNext()
        {
            Debug.Log("Before : " + currentIndex);

            if (characters == null || characters.Length == 0)
                return;

            if (currentIndex < characters.Length - 1)
            {
                currentIndex++;
            }

            Debug.Log("After : " + currentIndex);

            UpdateCharacterUI();
        }

        [Tooltip("왼쪽 버튼에서 호출합니다. 이전 캐릭터로 이동합니다.")]
        public void OnClickPrev()
        {
            Debug.Log("Before : " + currentIndex);

            if (characters == null || characters.Length == 0)
                return;

            if (currentIndex > 0)
            {
                currentIndex--;
            }

            Debug.Log("After : " + currentIndex);

            UpdateCharacterUI();
        }

        private void ClampCurrentIndex()
        {
            if (characters == null || characters.Length == 0)
            {
                currentIndex = 0;
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, characters.Length - 1);
        }

        private void UpdateCharacterUI()
        {
            Debug.Log("UpdateCharacterUI");

            if (characters == null || characters.Length == 0)
            {
                Debug.LogWarning("[CharacterSelection] characters 배열이 비어 있어서 캐릭터 선택 UI를 갱신할 수 없습니다.");

                if (leftCard != null)
                    leftCard.SetActive(false);

                if (rightCard != null)
                    rightCard.SetActive(false);

                return;
            }

            ClampCurrentIndex();

            CharacterData current = characters[currentIndex];

            if (centerCharImage != null)
                centerCharImage.sprite = current.charSprite;

            if (nameText != null)
                nameText.text = current.charName;

            if (descText != null)
                descText.text = current.description;

            if (attackSlider != null)
                attackSlider.value = current.attackStat;

            if (defenseSlider != null)
                defenseSlider.value = current.defenseStat;

            if (speedSlider != null)
                speedSlider.value = current.speedStat;

            UpdateLeftPreview();
            UpdateRightPreview();
        }

        private void UpdateLeftPreview()
        {
            bool hasLeftCharacter = currentIndex > 0;

            if (leftCard != null)
                leftCard.SetActive(hasLeftCharacter);

            if (!hasLeftCharacter)
                return;

            CharacterData leftCharacter = characters[currentIndex - 1];

            if (leftPreviewImage != null)
                leftPreviewImage.sprite = leftCharacter.charSprite;

            if (leftNameText != null)
                leftNameText.text = leftCharacter.charName;
        }

        private void UpdateRightPreview()
        {
            bool hasRightCharacter = currentIndex < characters.Length - 1;

            if (rightCard != null)
                rightCard.SetActive(hasRightCharacter);

            if (!hasRightCharacter)
                return;

            CharacterData rightCharacter = characters[currentIndex + 1];

            if (rightPreviewImage != null)
                rightPreviewImage.sprite = rightCharacter.charSprite;

            if (rightNameText != null)
                rightNameText.text = rightCharacter.charName;
        }
    }
}
