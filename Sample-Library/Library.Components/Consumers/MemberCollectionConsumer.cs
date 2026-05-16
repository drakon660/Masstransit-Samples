using Library.Contracts;
using MassTransit;

namespace Library.Components.Consumers;

public class MemberCollectionConsumer : IConsumer<AddBookToMemberCollection>
{
    public async Task Consume(ConsumeContext<AddBookToMemberCollection> context)
    {
        await context.Publish<BookAddedToMemberCollection>(new
        {
            context.Message.BookId,
            context.Message.MemberId
        });
    }
}