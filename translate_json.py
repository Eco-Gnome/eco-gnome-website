import json
import os
import requests

DEEPL_API_KEY = os.getenv("DEEPL_API_KEY")
DEEPL_API_URL = "https://api-free.deepl.com/v2/translate"

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
    params = {
        "auth_key": DEEPL_API_KEY,
        "text": text,
        "target_lang": target_language
    }
    response = requests.post(DEEPL_API_URL, data=params)

    if response.status_code != 200:
        print(f"⚠️ Erreur lors de la traduction en {target_language}: {response.status_code} - {response.text}")
        return text  # Retourne le texte original en cas d'erreur

    try:
        result = response.json()
        return result["translations"][0]["text"] if "translations" in result else text
    except json.JSONDecodeError:
        print(f"⚠️ Réponse invalide de DeepL pour {target_language}: {response.text}")
        return text  # Retourne le texte original en cas d'erreur de parsing

# Générer les fichiers traduits
for lang_code, lang_name in LANGUAGES.items():
    translated_data = {}
    for key, text in english_data.items():
        translated_data[key] = translate_text(text, lang_name)

    # Sauvegarde du fichier traduit
    output_path = f"ecocraft/wwwroot/assets/lang/{lang_code}.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(translated_data, f, indent=2, ensure_ascii=False)
    print(f"✅ Traduction en {lang_code} enregistrée: {output_path}")

os.system("git config --global user.name 'github-actions'")
os.system("git config --global user.email 'github-actions@github.com'")
os.system("git add ecocraft/wwwroot/assets/lang/*.json")
os.system("git commit -m 'Mise à jour des traductions' || echo 'Aucune modification'")
os.system("git push")
