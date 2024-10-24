using ecocraft.Models;
using System.Security.Cryptography;

namespace ecocraft.Extensions
{
	public static class UserExtensions
	{
		public static void GeneratePseudo(this User user, int length = 8)
		{
			const string chars = "0123456789";
			using (var rng = RandomNumberGenerator.Create())
			{
				var byteBuffer = new byte[length];

				// Remplir le buffer avec des octets aléatoires
				rng.GetBytes(byteBuffer);

				// Convertir les octets en caractères alphanumériques
				user.Pseudo = "user" + new string(byteBuffer.Select(b => chars[b % chars.Length]).ToArray());
			}
		}
	}
}
