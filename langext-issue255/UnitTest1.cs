using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;
using static LanguageExt.Prelude;

namespace langext_issue255
{
    public class UnitTest1
    {
        [Fact]
        public async Task no_gender_found_returns_not_found()
        {
            var repo = new Mock<IRepository>();
            repo.Setup(x => x.GetGenderByIdAsync(It.IsAny<Guid>())).ReturnsAsync(() => Option<Gender>.None);
            var controller = new TestController(repo.Object);

            var result = await controller.GetGenderByIdAsync(Guid.NewGuid());
            result.ShouldBeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task gender_found_returns_ok()
        {
            var repo = new Mock<IRepository>();
            repo.Setup(x => x.GetGenderByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Gender());
            var controller = new TestController(repo.Object);

            var result = await controller.GetGenderByIdAsync(Guid.NewGuid());
            result.ShouldBeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task default_guid_returns_not_found()
        {
            var repo = new Mock<IRepository>();
            repo.Setup(x => x.GetGenderByIdAsync(It.IsAny<Guid>())).ReturnsAsync(() => Option<Gender>.None);
            var controller = new TestController(repo.Object);

            var result = await controller.GetGenderByIdAsync(default(Guid));
            result.ShouldBeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task repo_exception_returns_internal_server_error()
        {
            var repo = new Mock<IRepository>();
            repo.Setup(x => x.GetGenderByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception("whoops"));
            var controller = new TestController(repo.Object);

            var result = await controller.GetGenderByIdAsync(Guid.NewGuid());
            result.ShouldBeOfType<ObjectResult>();
            ((ObjectResult) result).StatusCode.ShouldBe(500);
        }
    }

    public class TestController : Controller
    {
        private readonly IRepository _repository;

        public TestController(IRepository repository)
        {
            _repository = repository;
        }

        [SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
        public Task<IActionResult> GetGenderByIdAsync(Guid id) =>
            id.ToOption().MatchAsync(
                None: () => NotFound(),
                Some: genderId =>
                    _repository.GetGenderByIdAsync(genderId).ToTryOptionAsync().Match(
                        None: () => NotFound(),
                        Some: gender => Ok(gender),
                        Fail: e => (IActionResult) StatusCode(500, e)
                    )
            );

        //Doesn't work
        [SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
        public async Task<IActionResult> MonadicGetGenderByIdAsync(Guid id)
        {
            var program =
                from i in id.ToOption().ToTryOptionAsync()
                from g in _repository.GetGenderByIdAsync(i).ToTryOptionAsync()
                select g;

            return (await program()).Match(
                Some: gender => (IActionResult) Ok(gender),
                None: () => NotFound(),
                Fail: e => StatusCode(500, e)
            );
        }
    }

    public static class Global
    {
        public static Option<Guid> ToOption(this Guid id) =>
            id == default(Guid) ? Option<Guid>.None : Some(id);

        // ReSharper disable ConvertClosureToMethodGroup

        public static async Task<Either<TError, B>> Select<TError, A, B>(this Task<Either<TError, A>> self,
            Func<A, B> f)
            where TError : NewType<TError, string> =>
            (await self).Match(
                Right: r => Right<TError, B>(f(r)),
                Left: l => Left<TError, B>(l));

        public static async Task<Either<TError, C>> SelectMany<TError, A, B, C>(
            this Task<Either<TError, A>> self,
            Func<A, Task<Either<TError, B>>> bind,
            Func<A, B, C> project
        ) where TError : NewType<TError, string> =>
            await (await self).MatchAsync(
                Right: async a => (await bind(a)).Match(
                    Right: b => Right<TError, C>(project(a, b)),
                    Left: l => Left<TError, C>(l)),
                Left: l => Left<TError, C>(l));

        public static async Task<Either<TError, A>> Where<TError, A>(this Task<Either<TError, A>> self, Func<A, bool> f)
            where TError : NewType<TError, string> =>
            (await self).Match(
                Right: r => f(r)
                    ? r
                    : Either<TError, A>.Bottom,
                Left: l => l);

        // ReSharper restore ConvertClosureToMethodGroup
    }

    public interface IRepository
    {
        Task<Option<Gender>> GetGenderByIdAsync(Guid id);
    }

    public class Gender
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}