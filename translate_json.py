import json
import openai
import os

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
openai.api_key = OPENAI_API_KEY

LANGUAGES = {
    "fr": "French",
    "es": "Spanish",
    "de": "German",
    "ko": "Korean",
    "pt_BR": "Brazilian Portuguese",
    "zh_Hans": "Simplified Chinese",
    "ru": "Russian",
    "it": "Italian",
    "pt_PT": "Portuguese",
    "hu": "Hungarian",
    "ja": "Japanese",
    "nn": "Norwegian",
    "pl": "Polish",
    "nl": "Dutch",
    "ro": "Romanian",
    "da": "Danish",
    "cs": "Czech",
    "sv": "Swedish",
    "uk": "Ukrainian",
    "el": "Greek",
    "ar_sa": "Arabic",
    "vi": "Vietnamese",
    "tr": "Turkish"
}

with open("ecocraft/wwwroot/assets/lang/en_US.json", "r", encoding="utf-8") as f:
    english_data = json.load(f)

def translate_text(text, target_language):
    response = client.chat.completions.create(
        model="gpt-4",
        messages=[
            {"role": "system", "content": f"You are a translation assistant. You will translate a file for a website named Eco Gnome, that is a production & economy assistant for the video game called Eco. Translate the following json file to {LANGUAGES[target_language]}. Make sure you do not remove any existing line. Keep the original json structure:"},
            {"role": "user", "content": text}
        ]
    )
    return response.choices[0].message.content.strip()

for lang_code, lang_name in LANGUAGES.items():
    translated_data = {}
    for key, text in english_data.items():
        translated_data[key] = translate_text(text, lang_code)

    output_path = f"ecocraft/wwwroot/assets/lang/{lang_code}.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(translated_data, f, indent=2, ensure_ascii=False)
    print(f"✅ Traduction en {lang_name} enregistrée: {output_path}")

os.system("git config --global user.name 'github-actions'")
os.system("git config --global user.email 'github-actions@github.com'")
os.system("git add ecocraft/wwwroot/assets/lang/*.json")
os.system("git commit -m 'Mise à jour des traductions' || echo 'Aucune modification'")
os.system("git push")
