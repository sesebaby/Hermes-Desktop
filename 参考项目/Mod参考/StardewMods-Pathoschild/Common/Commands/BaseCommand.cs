using StardewModdingAPI;

namespace Pathoschild.Stardew.Common.Commands;

/// <summary>The base implementation for a console command implemented by Content Patcher.</summary>
internal abstract class BaseCommand : ICommand
{
    /*********
    ** Fields
    *********/
    /// <summary>Encapsulates monitoring and logging.</summary>
    protected readonly IMonitor Monitor;


    /*********
    ** Accessors
    *********/
    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description => this.GetDescription();


    /*********
    ** Public methods
    *********/
    /// <summary>Get a description for the command shown by the 'patch help' command.</summary>
    public abstract string GetDescription();

    /// <inheritdoc />
    public abstract void Handle(string[] args);


    /*********
    ** Protected methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="monitor">Encapsulates monitoring and logging.</param>
    /// <param name="name">The command's sub-name (e.g. the 'export' in 'patch export').</param>
    protected BaseCommand(IMonitor monitor, string name)
    {
        this.Monitor = monitor;
        this.Name = name;
    }
}
