using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OpenAI
{
    public class GptHandler : MonoBehaviour
    {
        private OpenAIApi openai = new OpenAIApi();

        public string lastMessage;
        public string lastResponse;
        public List<ChatMessage> messages = new List<ChatMessage>();
        //public List<ChatMessage> responses = new List<ChatMessage>();
        private string prompt = "너는 XR 기기 속 어시스턴트 K.A.R.V.I.S야. 주어진 텍스트를 읽고 이해해서, 상황에 맞는 간단하고 자연스러운 한국어 답변을 해줘. 답변은 짧고, 존댓말로 이야기하듯이 해.”";

        public async System.Threading.Tasks.Task SendReply(string text, System.Action onComplete = null)
        {
            var newMessage = new ChatMessage()
            {
                Role = "user",
                Content = text
            };

            newMessage.Content = prompt + "\n" + text; 
            lastMessage = newMessage.Content;
            messages.Add(newMessage);
            
            // Complete the instruction
            var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = "gpt-4o-mini",
                Messages = messages
            });

            if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
            {
                var response = completionResponse.Choices[0].Message;
                response.Content = response.Content.Trim();
                lastResponse = response.Content;
            }
            else
            {
                lastResponse = "대답이 생성되지 않았습니다.";
                Debug.LogWarning("[KARVIS] No text was generated from this prompt.");
            }
            
            onComplete?.Invoke();
        }
    }
}
