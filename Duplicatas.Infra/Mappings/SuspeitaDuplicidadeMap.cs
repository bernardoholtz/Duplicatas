using CustomerPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerPlatform.Infrastructure.Mappings;

public class SuspeitaDuplicidadeFisicaMap : IEntityTypeConfiguration<SuspeitaDuplicidade>
{
    public void Configure(EntityTypeBuilder<SuspeitaDuplicidade> builder)
    {
        builder.ToTable("Suspeitas_Duplicidade");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DetalhesSimilaridade)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Score)
            .IsRequired();

        builder.Property(x => x.DataDeteccao)
            .IsRequired();

        builder.HasIndex(x => x.IdOriginal);
        builder.HasIndex(x => x.IdSuspeito);
    }
}
