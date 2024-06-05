﻿using MediatR;
using MicroRabbit.Banking.Domain.CommandHandlers;
using MicroRabbit.Banking.Domain.Commands;
using MicroRabbit.Domain.Core.Bus;
using MicroRabbit.Infra.Bus;
using MicroRabbit.Transfer.Application.Interfaces;
using MicroRabbit.Transfer.Application.Services;
using MicroRabbit.Transfer.Data.Context;
using MicroRabbit.Transfer.Data.Repository;
using MicroRabbit.Transfer.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MicroRabbit.Infra.IoC;

public class TransferDependencyContainer
{
    public static void RegisterServices(IServiceCollection services)
    {
        // Domain Bus
        services.AddTransient<IEventBus, RabbitMQBus>();

        // Domain Banking Commands
        services.AddTransient<IRequestHandler<CreateTransferCommand, bool>, TransferCommandHandler>();

        // Application Services
        //services.AddTransient<IAccountService, AccountService>();
        services.AddTransient<ITransferService, TransferService>();

        //Data
        //services.AddTransient<IAccountRepository, AccountRepository>();
        services.AddTransient<ITransferRepository, TransferRepository>();
        //services.AddTransient<BankingDbContext>();
        services.AddTransient<TransferDbContext>();
    }
}