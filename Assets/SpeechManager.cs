using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Windows.Speech;
using Newtonsoft.Json.Linq;
using System.IO;
using System;

public class SpeechManager : MonoBehaviour
{
    public TMP_Text results;
    public Button speakButton;
    public GameObject man;
    private Animator manAnimator;
    private DictationRecognizer dictationRecognizer;
    private string recognizedText = "";
    private bool isDictationRunning = false;

    private const string openAIApiKey = "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"; // Replace with your OpenAI API Key
    private const string apiUrl = "https://api.openai.com/v1/chat/completions";
        string googleApiKey = "AIzaSyDrptAH25B0Bdj_Ik68nvM7bCsxfYLm9VM";

    private void Start()
    {
        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationHypothesis += OnDictationHypothesis;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;

        StartDictationRecognizer();

        manAnimator = man.GetComponent<Animator>();

        speakButton.onClick.AddListener(SpeakRecognizedText);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isDictationRunning)
            {
                StartDictationRecognizer();
            }
        }
    }

    private void StartDictationRecognizer()
    {
        if (!isDictationRunning)
        {
            dictationRecognizer.Start();
            isDictationRunning = true;
            Debug.Log("Dictation recognizer started.");
        }
    }

    private void StopDictationRecognizer()
    {
        if (isDictationRunning)
        {
            dictationRecognizer.Stop();
            isDictationRunning = false;
            Debug.Log("Dictation recognizer stopped.");
        }
    }

    private void OnDictationHypothesis(string text)
    {
        results.text = "Listening... " + text;
        Debug.Log("Dictation hypothesis: " + text);
    }

    private async void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        recognizedText = text;
        results.text = "You said: <b>" + text + "</b>";
        Debug.Log("Dictation result: " + text);

        // Automatically send the recognized text to ChatGPT and process the result
        await SendAndSpeakText(recognizedText);
    }

    private async Task SendAndSpeakText(string text)
    {
        // Get response from OpenAI API
        string aiResponse = await SendToOpenAI(text);

        if (!string.IsNullOrEmpty(aiResponse))
        {
            // Trigger talk animation
          

            // Get the audio from ElevenLabs API
            var audioClip = await GetAudioFromElevenLabs(aiResponse);

            if (audioClip != null)
            {
                PlayAudioClip(audioClip);
            }
            else
            {
                Debug.LogError("Failed to get audio from ElevenLabs.");
            }
        }
        else
        {
            Debug.LogError("No response received from OpenAI.");
        }
    }


    private async Task<string> SendToOpenAI(string text)
    {
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            Debug.LogError("API Key is missing!");
            return "API Key is missing!";
        }

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAIApiKey}");

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = text + ". Please keep your response under 50 words." }
                }
            };

            string jsonContent = JsonConvert.SerializeObject(requestBody);
            StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);

                if (responseObject.choices.Length > 0)
                {
                    return responseObject.choices[0].message.content.Trim();
                }
                else
                {
                    return "No response received.";
                }
            }
            else
            {
                Debug.LogError("Error: " + response.StatusCode + " - " + response.ReasonPhrase);
                return $"Error: {response.StatusCode} - {response.ReasonPhrase}";
            }
        }
    }

    private async Task<AudioClip> GetAudioFromGoogleTTS(string text)
    {
        string apiUrl = "https://texttospeech.googleapis.com/v1/text:synthesize?key=" + googleApiKey;

        var requestBody = new
        {
            input = new { text = text },
            voice = new { languageCode = "en-US", ssmlGender = "FEMALE" },
            audioConfig = new { audioEncoding = "MP3" }
        };

        string jsonContent = JsonConvert.SerializeObject(requestBody);

        using (HttpClient client = new HttpClient())
        {
            HttpContent httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(apiUrl, httpContent);

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseString);
                string audioContentBase64 = responseJson["audioContent"].ToString();
                byte[] audioBytes = System.Convert.FromBase64String(audioContentBase64);

                // Write the audio content to a temporary file
                string tempFilePath = Path.Combine(Application.persistentDataPath, "output.mp3");
                await File.WriteAllBytesAsync(tempFilePath, audioBytes);

                // Load the MP3 file into Unity's AudioClip
                using (WWW www = new WWW("file://" + tempFilePath))
                {
                    AudioClip clip = www.GetAudioClip(false, true, AudioType.MPEG);
                    while (!clip.isReadyToPlay)
                        await Task.Yield(); // Wait for the clip to load

                    return clip;
                }
            }
            else
            {
                Debug.LogError("Failed to get audio from Google TTS. Status Code: " + response.StatusCode);
                return null;
            }
        }
    }
    private async Task<AudioClip> GetAudioFromElevenLabs(string text)
    {
        string elevenLabsApiUrl = $"https://api.elevenlabs.io/v1/text-to-speech/N2lVS1w4EtoT3dr4eOWO/stream"; // Replace <voice-id> with your desired voice ID
        string elevenLabsApiKey = "sk_018c163ef1864e8deacb96d4ab633c4cc2087d90080c5b2d"; // Replace with your ElevenLabs API key

        var requestBody = new
        {
            text = text,
            model_id = "eleven_multilingual_v2",
            voice_settings = new
            {
                stability = 0.5f,
                similarity_boost = 0.8f,
                style = 0.0f,
                use_speaker_boost = true
            }
        };

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("xi-api-key", elevenLabsApiKey);

            HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            // Enable streaming the response
            HttpResponseMessage response = await client.PostAsync(elevenLabsApiUrl, httpContent);

            if (response.IsSuccessStatusCode)
            {
                byte[] audioBytes = await response.Content.ReadAsByteArrayAsync();

                // Save the audio stream to a local file
                string tempFilePath = Path.Combine(Application.persistentDataPath, "output.mp3");
                await File.WriteAllBytesAsync(tempFilePath, audioBytes);

                // Load the MP3 file into Unity's AudioClip
                using (WWW www = new WWW("file://" + tempFilePath))
                {
                    AudioClip clip = www.GetAudioClip(false, true, AudioType.MPEG);
                    while (!clip.isReadyToPlay)
                        await Task.Yield(); // Wait for the clip to load

                    return clip;
                }
            }
            else
            {
                Debug.LogError("Failed to get audio from ElevenLabs. Status Code: " + response.StatusCode);
                Debug.LogError(response.Content.ReadAsStringAsync().Result);
                return null;
            }
        }
    }

    private void PlayAudioClip(AudioClip clip)
    {
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
        manAnimator.SetTrigger("Talk");
        // Ensure the animation stops or transitions after the audio has finished playing
        StartCoroutine(WaitForAudioToFinish(audioSource));
    }

    private IEnumerator WaitForAudioToFinish(AudioSource audioSource)
    {
        // Wait until the audio source has finished playing
        while (audioSource.isPlaying)
        {
            yield return null;
            StopDictationRecognizer();
        }

        // Trigger the stop animation (assuming you have a "Idle" trigger)
        manAnimator.CrossFade("Idle", 0.02f);
       StartDictationRecognizer();
        // Clean up the audio source component
        Destroy(audioSource);
    }

  private async void SpeakRecognizedText()
{
    if (!string.IsNullOrEmpty(recognizedText))
    {
        // Stop DictationRecognizer to turn off the microphone
        StopDictationRecognizer();

        // Await the SendAndSpeakText method
        await SendAndSpeakText(recognizedText);
    }
}


    private void OnDictationComplete(DictationCompletionCause cause)
    {
        if (cause != DictationCompletionCause.Complete)
        {
            Debug.LogError("Dictation completed unexpectedly: " + cause);
            RestartDictationRecognizer();
        }
        else
        {
            Debug.Log("Dictation completed successfully.");
            RestartDictationRecognizer();
        }
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + ", HResult: " + hresult);
        RestartDictationRecognizer();
    }

    private void RestartDictationRecognizer()
    {
        StopDictationRecognizer();
        StartDictationRecognizer();
    }

    private void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Dispose();
        }
    }

    // OpenAI API response model class
    [Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices;
    }

    [Serializable]
    public class Choice
    {
        public Message message;
    }

    [Serializable]
    public class Message
    {
        public string role;
        public string content;
    }
}
