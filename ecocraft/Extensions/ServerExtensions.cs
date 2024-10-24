using ecocraft.Models;
using System.Security.Cryptography;

namespace ecocraft.Extensions
{
	public static class ServerExtensions
	{
		public static void GenerateJoinCode(this Server server, int length = 8)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			using (var rng = RandomNumberGenerator.Create())
			{
				var byteBuffer = new byte[length];

				// Remplir le buffer avec des octets aléatoires
				rng.GetBytes(byteBuffer);

				// Convertir les octets en caractères alphanumériques
				server.JoinCode = new string(byteBuffer.Select(b => chars[b % chars.Length]).ToArray());
			}
		}
	}
}
