using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.TimeZones;
namespace EdgeTime;
// UTC is anchored to a server response and advances with a monotonic clock.
internal sealed class NetworkClock {
 readonly object gate=new(); readonly SemaphoreSlim syncGate=new(1,1); DateTimeOffset anchor; long anchorTick; bool calibrated; string source="";
 public bool Calibrated {get{lock(gate)return calibrated;}}
 public string Source {get{lock(gate)return source;}}
 public DateTimeOffset? UtcNow {get{lock(gate){return calibrated?anchor+Stopwatch.GetElapsedTime(anchorTick):null;}}}
 public DateTimeOffset? InZone(string id){var now=UtcNow;if(now==null)return null;var zone=DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)??DateTimeZone.Utc;return Instant.FromDateTimeOffset(now.Value).InZone(zone).ToDateTimeOffset();}
 public static string NormalizeZone(string id){if(DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)!=null)return id;return TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping.TryGetValue(id,out var mapped)?mapped:"Etc/UTC";}
 public async Task<string> CalibrateAsync(){await syncGate.WaitAsync();try{Exception? last=null;foreach(var host in new[]{"time.cloudflare.com","ntp.aliyun.com"}){try{var reading=await QueryAsync(host);lock(gate){anchor=reading.Utc;anchorTick=reading.Tick;calibrated=true;source=host;}return host;}catch(Exception ex){last=ex;}}throw new InvalidOperationException("未能连接时间服务器，请检查网络后重试。",last);}finally{syncGate.Release();}}
 readonly record struct Reading(DateTimeOffset Utc,long Tick);
 static async Task<Reading> QueryAsync(string host){using var udp=new UdpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));var addresses=await Dns.GetHostAddressesAsync(host,timeout.Token);var address=Array.Find(addresses,a=>a.AddressFamily==AddressFamily.InterNetwork)??throw new InvalidOperationException("无法解析时间服务器。");udp.Connect(address,123);byte[] request=new byte[48];request[0]=0x23;RandomNumberGenerator.Fill(request.AsSpan(40,8));long sent=Stopwatch.GetTimestamp();await udp.SendAsync(request,timeout.Token);var response=await udp.ReceiveAsync(timeout.Token);long received=Stopwatch.GetTimestamp();var data=response.Buffer;if(data.Length<48||(data[0]&7)!=4||(data[0]>>6)==3||data[1]==0||data[1]>15||!data.AsSpan(24,8).SequenceEqual(request.AsSpan(40,8)))throw new InvalidOperationException("时间服务器返回了无效数据。");var receive=ReadTime(data,32);var transmit=ReadTime(data,40);var processing=transmit-receive;var roundtrip=Stopwatch.GetElapsedTime(sent,received);if(processing<TimeSpan.Zero||processing>roundtrip+TimeSpan.FromMilliseconds(10)||roundtrip>TimeSpan.FromSeconds(3))throw new InvalidOperationException("时间服务器返回的时延无效。");var transit=roundtrip-processing;if(transit<TimeSpan.Zero)transit=TimeSpan.Zero;return new Reading(transmit+TimeSpan.FromTicks(transit.Ticks/2),received);}
 static DateTimeOffset ReadTime(byte[] data,int index){uint seconds=((uint)data[index]<<24)|((uint)data[index+1]<<16)|((uint)data[index+2]<<8)|data[index+3];uint fraction=((uint)data[index+4]<<24)|((uint)data[index+5]<<16)|((uint)data[index+6]<<8)|data[index+7];long eraSeconds=seconds<2208988800U?(long)seconds+4294967296L:seconds;long unixSeconds=eraSeconds-2208988800L;long ticks=(long)((ulong)fraction*10000000UL>>32);return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).AddTicks(ticks);}
}
