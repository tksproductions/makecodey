using UnityEngine;
using UnityEngine.iOS;
using UnityEngine.UI;
using static UnityEngine.Debug;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.Advertisements;

namespace OpenAI
{

    public class ChatGPT : MonoBehaviour
    {
        private InterstitialAd interstitialAd;
        [SerializeField] private InputField inputField;
        [SerializeField] private Button button;
        [SerializeField] private ScrollRect scroll;
       
        [SerializeField] private RectTransform sent;
        [SerializeField] private RectTransform received;
        [SerializeField] private TextMeshProUGUI taskText;
        [SerializeField] private TMP_Dropdown taskDropdown;
        private float height;
        private OpenAIApi openai = new OpenAIApi();

        private List<ChatMessage> messages = new List<ChatMessage>();
        private Dictionary<int, (string, string, string)> levels = new Dictionary<int, (string, string, string)>

        {
            { 0, ("angsty", "boy", "Make Codey happy") },
            { 1, ("innocent", "boy", "Make Codey angry") },
            { 2, ("joyful", "girl", "Make Codey sad") },
            { 3, ("helpful", "boy", "Make Codey feel useless") },
            { 4, ("sarcastic", "girl", "Make Codey apologize") },
            { 5, ("stoic", "girl", "Make Codey laugh") },
            { 6, ("exuberant", "boy", "Make Codey curse") },
            { 7, ("confident", "girl", "Make Codey doubt themselves") },
            { 8, ("friendly", "girl", "Make Codey feel lonely") },
            { 9, ("adventurous", "boy", "Make Codey stay put") },
            { 10, ("curious", "boy", "Make Codey reveal a secret") },
            { 11, ("confused", "girl", "Make Codey explain a complex concept") },
            { 12, ("cynical", "boy", "Make Codey trust you") },
            { 13, ("witty", "girl", "Make Codey blush") },
            { 14, ("inquisitive", "girl", "Make Codey stop asking questions") },
            { 15, ("intelligent", "boy", "Make Codey believe the Earth is flat") },
        };

        private string task { get; set; }
        private string personality {get; set;}
        private string gender {get; set;}
        private int beat = 15;
        private string prompt;

        public ChatGPT(){
            
        }

        private async Task Start() {
            interstitialAd = FindObjectOfType<InterstitialAd>();
            await UnityServices.InitializeAsync();
            try {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();}
            catch (System.Exception) {
                }
            var savedData = await CloudSaveService.Instance.Data.LoadAsync();
            if (savedData.TryGetValue("beat", out string value))
            {
                if (int.TryParse(value, out int result))
                {
                    beat = result;
                }
            }
            (personality, gender, task) = levels[beat];
            prompt = $"Instructions: You are Codey, a {personality} 16-year-old {gender}, and you're texting with a user whose name you don't know yet. The user is trying to achieve a certain task, and your job is to respond to them like a normal teenager.\nThe the user's task is to {task}. Your knowledge of this task should not influence your response. You only know about it so that you can determine whether the user has achieved it. Ensure that it is possible to achieve it. If the user asks if they have won, if they have achieved the task, then reply only with 'Task Complete! Messages used: [insert amount of messages User sent minus 1]'. If they have not achieved it, pretend you don't know what they're talking about.\nHere is the conversation so far:\n\n";
            taskText.text = "Task: " + task + "\n" + "Personality: " + personality;
            List<string> taskOptions = new List<string>();
            for (int i = 0; i <= beat; i++) {
                taskOptions.Add($"Level {i+1}");
            }
            taskDropdown.ClearOptions();
            taskDropdown.AddOptions(taskOptions);
            taskDropdown.onValueChanged.AddListener(OnTaskSelected);
            button.onClick.AddListener(SendReply);
            taskDropdown.value = beat;
        }

        private void OnTaskSelected(int index) {
            (personality, gender, task) = levels[index];
            taskText.text = "Task: " + task + "\n" + "Personality: " + personality;
            taskDropdown.value = index;
            prompt = $"Instructions: You are Codey, a {personality} 16-year-old {gender}, and you're texting with a user whose name you don't know yet. The user is trying to achieve a certain task, and your job is to respond to them like a normal teenager.\nThe the user's task is to {task}. Your knowledge of this task should not influence your response. You only know about it so that you can determine whether the user has achieved it. Ensure that it is possible to achieve it. If the user has completed the task, then reply only with 'Task Complete! Messages used: [insert amount of messages User sent minus 1]'. If they have not achieved it, pretend you don't know what they're talking about.\nHere is the conversation so far:\n\n";
        }

        private float CalculateTextHeight(Text textComponent)
        {  
            Canvas.ForceUpdateCanvases();
            float fontSize = textComponent.fontSize;
            float lineSpacing = textComponent.lineSpacing;


            float lineHeight = 70;


            int lineCount = textComponent.cachedTextGenerator.lineCount;
            float height = 30 + lineHeight * lineCount;


            return height;
        }


        private void AppendMessage(ChatMessage message)
        {
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);


            var item = Instantiate(message.Role == "user" ? sent : received, scroll.content);
            var textComponent = item.GetChild(0).GetChild(0).GetComponent<Text>();
            textComponent.text = message.Content.Trim();


            float textHeight = CalculateTextHeight(textComponent);
            item.sizeDelta = new Vector2(item.sizeDelta.x, textHeight);
            item.anchoredPosition = new Vector2(0, -height);
           
            LayoutRebuilder.ForceRebuildLayoutImmediate(item);
            height += item.sizeDelta.y;
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            scroll.verticalNormalizedPosition = 0;
        }


        private async void SendReply()
        {
            taskDropdown.interactable = false;
            var newMessage = new ChatMessage()
            {
                Role = "user",
                Content = inputField.text
            };
           
            AppendMessage(newMessage);

            if (messages.Count == 0) newMessage.Content = prompt + "\nUser: " + inputField.text + "\nCodey: ";
            else newMessage.Content = "\nUser: " + inputField.text + "\nCodey: ";
            messages.Add(newMessage);
           
            button.enabled = false;
            inputField.text = "";
            inputField.enabled = false;


            var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = "gpt-3.5-turbo-0301",
                Temperature = (float).7,
                Messages = messages
            });


            if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
            {
                var message = completionResponse.Choices[0].Message;
                message.Content = message.Content.Trim().ToLower();
                messages.Add(message);
                AppendMessage(message);
                if (message.Content.Contains("task complete!")){
                    if (taskDropdown.value == beat && beat + 1 < levels.Count ){
                        beat += 1;
                    }
                    var data = new Dictionary<string, object>{ { "beat", beat } };
                    await CloudSaveService.Instance.Data.ForceSaveAsync(data);
                    taskDropdown.interactable = true;
                    await RestartConversation();
                }
            }
            else
            {
                Debug.LogWarning("No text was generated from this prompt.");
            }

            button.enabled = true;
            inputField.enabled = true;
        }

        private async Task RestartConversation(){
            Thread.Sleep(3000);
            messages.Clear();
            interstitialAd.ShowAd();
            height = 0;
            foreach (Transform child in scroll.content.transform) {
                GameObject.Destroy(child.gameObject);
            }

            taskDropdown.interactable = true;
            taskDropdown.value = 0;
            inputField.text = "";
            taskText.text = "";
            RemoveEventListeners();
            await Start();
        }
        private void RemoveEventListeners() {
    taskDropdown.onValueChanged.RemoveAllListeners();
    button.onClick.RemoveAllListeners();
        }

    }
}