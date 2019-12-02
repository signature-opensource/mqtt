﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CK.MQTT.Sdk.Formatters;
using CK.MQTT.Sdk.Packets;

namespace CK.MQTT.Sdk
{
	internal class PacketManager : IPacketManager
	{
		readonly IDictionary<MqttPacketType, IFormatter> formatters;

		public PacketManager (params IFormatter[] formatters)
			: this ((IEnumerable<IFormatter>)formatters)
		{
		}

		public PacketManager (IEnumerable<IFormatter> formatters)
		{
			this.formatters = formatters.ToDictionary (f => f.PacketType);
		}

		public async Task<IPacket> GetPacketAsync (byte[] bytes)
		{
			var packetType = (MqttPacketType)bytes.Byte (0).Bits (4);
			var formatter = default (IFormatter);

			if (!formatters.TryGetValue (packetType, out formatter))
				throw new MqttException (Properties.Resources.GetString("PacketManager_PacketUnknown"));

			var packet = await formatter.FormatAsync (bytes)
				.ConfigureAwait(continueOnCapturedContext: false);

			return packet;
		}

		public async Task<byte[]> GetBytesAsync (IPacket packet)
		{
			var formatter = default (IFormatter);

			if (!formatters.TryGetValue (packet.Type, out formatter))
				throw new MqttException (Properties.Resources.GetString("PacketManager_PacketUnknown"));

			var bytes = await formatter.FormatAsync (packet)
				.ConfigureAwait(continueOnCapturedContext: false);

			return bytes;
		}
	}
}