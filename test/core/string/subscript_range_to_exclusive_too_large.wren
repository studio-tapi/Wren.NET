// skip: Range subscripts for strings don't currently work.
var a = "123"
a[1...4] // expect runtime error: Range end out of bounds.
