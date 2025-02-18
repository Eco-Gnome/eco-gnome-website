import json
import os
import requests

def flatten_json_values(nested_json):
    """
    Transforme un JSON imbriqué en une liste de valeurs, dans l'ordre de parcours.
    """
    values_list = []

    def extract_values(obj):
        if isinstance(obj, dict):
            for value in obj.values():
                extract_values(value)
        else:
            values_list.append(f'{obj}')

    extract_values(nested_json)
    return values_list

# Charger le fichier JSON et extraire les valeurs
def convert_json_to_txt(json_file, output_txt):
    with open(json_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    values = flatten_json_values(data)

    with open(output_txt, "w", encoding="utf-8") as f:
        f.write("\n".join(values))  # Écriture en une seule ligne

    print(f"✅ Conversion terminée : {output_txt}")

def replace_json_values(nested_json, values):
    """
    Remplace les valeurs du JSON imbriqué par celles de la liste fournie, dans l'ordre.
    """
    index = 0

    def inject_values(obj):
        nonlocal index
        if isinstance(obj, dict):
            for key in obj:
                obj[key] = inject_values(obj[key])
        else:
            if index < len(values):
                obj = values[index]
                index += 1
        return obj

    inject_values(nested_json)
    return nested_json

# Charger le fichier JSON et modifier ses valeurs
def modify_json_with_txt(json_file, txt_file, output_json):
    with open(json_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    with open(txt_file, "r", encoding="utf-8") as f:
        values = f.read().strip().split("\n")

    modified_data = replace_json_values(data, values)

    with open(output_json, "w", encoding="utf-8") as f:
        json.dump(modified_data, f, indent=4, ensure_ascii=False)

    print(f"✅ Mise à jour terminée : {output_json}")

def translate_text_deepl(text, target_lang):
    """
    Envoie un texte complet à l'API DeepL pour traduction.
    """
    DEEPL_API_KEY = os.getenv("DEEPL_API_KEY")
    DEEPL_API_URL = "https://api-free.deepl.com/v2/translate"

    params = {
        "auth_key": DEEPL_API_KEY,
        "text": text,
        "target_lang": target_lang,
        "split_sentences": "1",
        "preserve_formatting": "1",
        "formality": "prefer_more"
    }
    response = requests.post(DEEPL_API_URL, data=params)

    if response.status_code != 200:
        print(f"⚠️ Erreur lors de la traduction : {response.status_code} - {response.text}")
        return text  # Retourne le texte original en cas d'erreur

    try:
        result = response.json()
        return result["translations"][0]["text"] if "translations" in result else text
    except json.JSONDecodeError:
        print(f"⚠️ Réponse invalide de DeepL : {response.text}")
        return text


def translate_txt_file(input_txt, output_txt, target_lang):
    """
    Traduit un fichier .txt via DeepL en une seule requête et enregistre la sortie.
    """
    with open(input_txt, "r", encoding="utf-8") as f:
        text = f.read().strip()

    translated_text = translate_text_deepl(text, target_lang)

    with open(output_txt, "w", encoding="utf-8") as f:
        f.write(translated_text)

    print(f"✅ Traduction terminée : {output_txt}")


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

convert_json_to_txt("ecocraft/wwwroot/assets/lang/en_US.json", "ecocraft/wwwroot/assets/lang/en_US.txt")

for lang_code, lang_name in LANGUAGES.items():
    translate_txt_file("ecocraft/wwwroot/assets/lang/en_US.txt", f"ecocraft/wwwroot/assets/lang/{lang_code}.txt", lang_code)
    modify_json_with_txt("ecocraft/wwwroot/assets/lang/en_US.json", f"ecocraft/wwwroot/assets/lang/{lang_code}.txt", f"ecocraft/wwwroot/assets/lang/{lang_code}.json")
