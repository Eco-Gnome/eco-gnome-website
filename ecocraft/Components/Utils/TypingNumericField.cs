using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ecocraft.Components.Utils;

// MudNumericField réécrit son texte depuis sa valeur à chaque rendu du parent, même en pleine frappe : le recalcul
// d'un prix remplaçait « 0. » ou « .0 » par « 0 », et la suite de la saisie partait de là. Tant que le joueur tape,
// on garde son texte s'il donne déjà la valeur courante ; une valeur vraiment différente (bornes, autre source)
// le remplace toujours. À la sortie du champ, le formatage habituel reprend (culture, arrondi).
public class TypingNumericField<T> : MudNumericField<T>
{
    private bool _typing;

    protected override Task SetTextAndUpdateValueAsync(string? text, bool updateValue = true)
    {
        // Le texte tiré de la valeur passe ici avec updateValue = false : seule la saisie du joueur compte.
        if (updateValue)
        {
            _typing = true;
        }

        return base.SetTextAndUpdateValueAsync(text, updateValue);
    }

    protected override Task UpdateTextPropertyAsync(bool updateValue)
    {
        if (_typing && EqualityComparer<T>.Default.Equals(ConvertGet(ReadText), ReadValue))
        {
            return Task.CompletedTask;
        }

        return base.UpdateTextPropertyAsync(updateValue);
    }

    protected override Task OnBlurredAsync(FocusEventArgs obj)
    {
        _typing = false;
        return base.OnBlurredAsync(obj);
    }
}
