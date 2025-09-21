using System.Runtime.CompilerServices;
using Duyao.TelegramFile.Entity;
using telegram.bot.webfile.Helper;
using TL;
using WTelegram;

namespace Duyao.TelegramFile.Helper;

public static class TelegramClientExt
{
    public static async Task WriteAsyncEnumerableBytes(this Stream stream, IAsyncEnumerable<byte[]> largeData)
    {
        await foreach (var bytes in largeData)
        {
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync(); // 确保数据及时发送到客户端
        }
    }

    public static async IAsyncEnumerable<byte[]> YieldFileAsync(
        this WTelegram.Client telegramClient,
        InputDocumentFileLocation? location,
        long fromBytes,
        long untilBytes,
        long fileSize,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        
        #region CalculateChunkParameters
        untilBytes = Math.Min(untilBytes, fileSize - 1);
        var offset = fromBytes - fromBytes % chunkSize;
        var firstPartCut = fromBytes - offset;
        var lastPartCut = untilBytes % chunkSize + 1;
        // var reqLength = untilBytes - fromBytes + 1;
        var partCount = (long)Math.Ceiling((double)untilBytes / chunkSize) -
                        (long)Math.Floor((double)offset / chunkSize);
        #endregion 
        
        var currentPart = 1;
        var local = telegramClient;
        while (currentPart <= partCount && !cancellationToken.IsCancellationRequested) // Check for cancellation
        {
            Upload_FileBase? getFileRequest;
            while (true)
                try
                {
                    getFileRequest = await local.Upload_GetFile(location
                        , offset
                        , chunkSize
                    );
                    break;
                }
                catch (RpcException rpcExp)
                {
                    if (rpcExp.Code == 303) local = await local.GetClientForDC(rpcExp.X);
                }

            if (getFileRequest is Upload_File file)
            {
                var chunk = file.bytes;
                if (chunk != null && chunk.Length > 0)
                {
                    byte[] chunkToYield;

                    if (partCount == 1)
                        // chunkToYield = chunk.SubArray(firstPartCut, chunk.Length - firstPartCut - lastPartCut);
                        chunkToYield = Utils.SubArray(chunk, firstPartCut, lastPartCut);
                    else if (currentPart == 1)
                        // chunkToYield = chunk.SubArray(firstPartCut); // Use extension method or manual slicing
                        chunkToYield = Utils.SubArray(chunk, firstPartCut);
                    else if (currentPart == partCount)
                        // chunkToYield = chunk.SubArray(0, chunk.Length - lastPartCut); // Use extension method or manual slicing
                        chunkToYield = Utils.SubArray(chunk, 0, lastPartCut);
                    else
                        chunkToYield = chunk;

                    yield return chunkToYield;
                    currentPart++;
                    offset += chunkSize;
                }
                else
                {
                    // _logger.LogWarning($"Empty chunk received for file ID: {fileId}, part: {currentPart}");
                    break; // Or handle differently
                }
            }
            else
            {
                break; // Or handle differently
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    // public static (long Offset, long FirstPartCut, long LastPartCut, long ReqLength, long PartCount)
    //     CalculateChunkParameters(long fromBytes, long untilBytes, long fileSize, int chunkSize = 1024 * 1024)
    // {
    //     untilBytes = Math.Min(untilBytes, fileSize - 1);
    //
    //     var offset = fromBytes - fromBytes % chunkSize;
    //     var firstPartCut = fromBytes - offset;
    //     var lastPartCut = untilBytes % chunkSize + 1;
    //
    //     var reqLength = untilBytes - fromBytes + 1;
    //     var partCount = (long)Math.Ceiling((double)untilBytes / chunkSize) -
    //                     (long)Math.Floor((double)offset / chunkSize);
    //
    //     return (offset, firstPartCut, lastPartCut, reqLength, partCount);
    // }

    public static async Task<TelegramGetMessageMediaResult> GetMessageMedia(
        this Client telegramClient
        , long chatId
        , int chatMessageId
        , bool isFromChannel
        )
    {
        var ret = new TelegramGetMessageMediaResult();
        Messages_MessagesBase? msg;
        
        if (isFromChannel)
        {
            var inputChannel = new InputChannel(chatId, 0);
            var channels = await telegramClient.Channels_GetChannels(new InputChannelBase[] { inputChannel });
                
            if (channels.chats.Count > 0 && channels.chats.First().Value is Channel channel)
            {
                msg = await telegramClient.GetMessages(channel
                    , [new InputMessageID { id = chatMessageId }]
                );
            }
            else
            {
                ret.Success = false;
                ret.Message = "expect channel but not exist";
                return ret;
            }
        }
        else
        {
            var chat = new InputPeerChat(chatId);
            msg = await telegramClient.GetMessages(chat
                , [new InputMessageID { id = chatMessageId }]
            );
        }

        if (msg.Messages.Length > 0 && msg.Messages.First() is Message m)
        {
            if (m.media == null)
            {
                ret.Success = false;
                ret.Message = "Message does not contain media";
                return ret;
            }
            if (m.media is MessageMediaDocument mediaDocument)
            {
                var doc = mediaDocument.document as Document;
                if (doc != null)
                {
                    ret.Type = TelegramGetMessageMediaResult.TelegramMessageMediaType.Document;
                    ret.DocumentInfo = doc;
                    return ret;
                }
            }
            if (m.media is MessageMediaPhoto mediaPhoto)
            {
                var photo = mediaPhoto.photo as Photo;
                if (photo != null)
                {
                    ret.Type = TelegramGetMessageMediaResult.TelegramMessageMediaType.Photo;
                    ret.PhotoInfo = photo;
                    return ret;
                }
            }
            ret.Success = false;
            ret.Message = "Unsupported file type for download";
            return ret;
        }

        ret.Success = false;
        ret.Message = "Media not found";
        return ret;
    }
}