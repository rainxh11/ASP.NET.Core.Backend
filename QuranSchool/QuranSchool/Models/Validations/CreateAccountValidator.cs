﻿using FluentValidation;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class CreateAccountValidator : AbstractValidator<CreateAccountModel>
{
    public CreateAccountValidator(IConfiguration config)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Account Name cannot be empty!")
            .MinimumLength(3).WithMessage("Account Name Minimum length is 3 characters!");
        //.Matches(@"^[u0621-\u064Aa-zA-Z\s]*$")
        //.WithMessage("Special characters & digits are not allowed in Account Name!");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username cannot be empty!")
            .MinimumLength(5).WithMessage("Username length is 5 characters minimum!")
            .Matches(@"^[a-zA-Z0-9]*$").WithMessage("Special characters & empty spaces are not allowed in Username!")
            .NotEqual(config["Google:DemoUser"]);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password cannot be empty!")
            .MinimumLength(8).WithMessage("Password length is 8 characters minimum!")
            .MaximumLength(20).WithMessage("Password maximum length is 20 characters!");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password cannot be empty!")
            .MinimumLength(8).WithMessage("Password length is 8 characters minimum!")
            .Equal(x => x.ConfirmPassword).WithMessage("Passowrd & Confirmation are not equal!")
            .MaximumLength(20).WithMessage("Password maximum length is 20 characters!");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Description maximum length is 100 characters!");
    }
}